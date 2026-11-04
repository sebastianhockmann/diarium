using System.Text;
using System.Text.RegularExpressions;
using DiariumBookStudio.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiariumBookStudio.Services;

/// <summary>
/// Erzeugt book.tex. Das Layout steckt in diarybook.sty (Templates-Ordner bzw.
/// eigene Version im Projektordner); hier werden nur Daten und Fotolayout geschrieben.
/// </summary>
public static class LatexExporter
{
    private static readonly CultureInfo German = new("de-DE");

    /// <summary>
    /// Führt den Export auf einem eigenen STA-Thread aus (WPF-Rendering der Karten braucht STA),
    /// damit die Oberfläche während Kartendownloads nicht einfriert.
    /// </summary>
    public static Task<string> ExportAsync(BookProject project, string? outputTexPath = null, IProgress<string>? progress = null,
        PreflightReport? report = null)
    {
        var tcs = new TaskCompletionSource<string>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(Export(project, outputTexPath, progress, report)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <param name="report">Sammelt Probleme für die Vorabprüfung (fehlende Bilder, Auflösung, ungeprüfte Einträge).</param>
    public static string Export(BookProject project, string? outputTexPath = null, IProgress<string>? progress = null,
        PreflightReport? report = null)
    {
        var texPath = string.IsNullOrWhiteSpace(outputTexPath)
            ? Path.Combine(project.OutputFolder, "book.tex")
            : outputTexPath;

        var outputDir = Path.GetDirectoryName(texPath) ?? project.OutputFolder;
        var imageDir = Path.Combine(outputDir, "images");
        Directory.CreateDirectory(imageDir);

        var logPath = Path.Combine(outputDir, "map_debug.txt");
        File.WriteAllText(logPath, $"DiariumBookStudio Export-Protokoll {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", Encoding.UTF8);
        void Log(string message)
        {
            try { File.AppendAllText(logPath, message + "\n", Encoding.UTF8); } catch (IOException) { }
        }

        CopyStyle(project, outputDir);
        var geometry = GeometryFor(project);
        WriteGeometry(outputDir, geometry);
        var maps = new MapService(Path.Combine(project.ProjectRoot, "cache", "tiles"), Log, project.GrayAccents);

        var entries = project.BookEntries.OrderBy(e => e.Date ?? DateTime.MaxValue).ThenBy(e => e.Key).ToList();

        // Einträge ganz ohne Inhalt (kein Titel, kein Text, keine Fotos) weglassen.
        // Nur mit Titel werden sie als Kurznotiz gesetzt (siehe AppendEntry).
        foreach (var empty in entries.Where(e => IsEmpty(e) && string.IsNullOrWhiteSpace(e.Title)).ToList())
        {
            Log($"Leerer Eintrag ausgelassen: {empty.DisplayTitle}");
            entries.Remove(empty);
        }
        if (entries.Count == 0)
            throw new InvalidOperationException(project.Year is { } year
                ? $"Für das Jahr {year} gibt es keine Einträge."
                : "Das Projekt enthält keine Einträge.");
        foreach (var notReviewed in entries.Where(e => !e.Done))
            report?.Add(PreflightSeverity.Hinweis, notReviewed,"Eintrag ist noch nicht als geprüft markiert.");

        var tex = new StringBuilder();
        tex.AppendLine("% Automatisch erzeugt mit DiariumBookStudio – Layout siehe diarybook.sty");
        tex.AppendLine("% Kompilieren: lualatex book.tex (zweimal, wegen Inhaltsverzeichnis)");
        tex.AppendLine("\\documentclass[11pt,twoside,openany]{scrbook}");
        tex.AppendLine("\\input{bookgeometry}");
        var options = new List<string>();
        if (project.PrintVersion) options.Add("print");
        if (project.GrayAccents) options.Add("gray");
        tex.AppendLine(options.Count == 0 ? "\\usepackage{diarybook}" : $"\\usepackage[{string.Join(",", options)}]{{diarybook}}");
        tex.AppendLine($"\\hypersetup{{pdftitle={{{LatexText.Escape(project.BookTitle)}}}}}");
        tex.AppendLine("\\begin{document}");
        tex.AppendLine("\\frontmatter");
        tex.AppendLine($"\\DiaryTitlePage{{{LatexText.Escape(project.BookTitle)}}}{{{LatexText.Escape(project.BookSubtitle)}}}{{{LatexText.Escape(BuildDateRange(entries))}}}");
        tex.AppendLine("\\tableofcontents");
        tex.AppendLine("\\mainmatter");

        var done = 0;
        foreach (var month in entries.GroupBy(e => e.Date is { } d ? new DateTime(d.Year, d.Month, 1) : (DateTime?)null))
        {
            var monthEntries = month.ToList();
            var monthName = month.Key?.ToString("MMMM", German) ?? "Ohne Datum";
            var year = month.Key?.Year.ToString() ?? string.Empty;
            // Einträge zuerst erzeugen: dabei werden Ortsnamen ermittelt, die in die Monatsstatistik einfließen.
            var monthTex = new StringBuilder();
            foreach (var entry in monthEntries)
            {
                progress?.Report($"Eintrag {++done} von {entries.Count}: {entry.DisplayTitle}");
                AppendEntry(monthTex, entry, imageDir, maps, Log, report, geometry);
            }

            tex.AppendLine();
            tex.AppendLine($"\\DiaryMonth{{{monthName}}}{{{year}}}{{{LatexText.Escape(BuildMonthStats(monthEntries))}}}");
            tex.Append(monthTex);
        }

        tex.AppendLine("\\DiaryFinish");
        tex.AppendLine("\\end{document}");
        File.WriteAllText(texPath, tex.ToString(), new UTF8Encoding(false));
        return texPath;
    }

    private static void AppendEntry(StringBuilder tex, EntryModel entry, string imageDir, MapService maps, Action<string> log,
        PreflightReport? report, BookGeometry geometry)
    {
        var longDate = entry.Date?.ToString("dddd, d. MMMM yyyy", German) ?? entry.DateText;
        var shortDate = entry.Date?.ToString("d. MMMM", German) ?? entry.DateText;
        var title = string.IsNullOrWhiteSpace(entry.Title) ? shortDate : entry.Title;
        var toc = string.IsNullOrWhiteSpace(entry.Title) ? shortDate : $"{shortDate} · {entry.Title}";

        var isShortEntry = IsEmpty(entry);

        string? mapFile = null;
        if (MapService.TryParseLatLon(entry.Location, out var lat, out var lon))
        {
            if (!isShortEntry)
                mapFile = maps.CreateMap(lat, lon, imageDir);
            if (string.IsNullOrWhiteSpace(entry.PlaceName))
                entry.PlaceName = maps.ReverseGeocode(lat, lon) ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(entry.Location))
        {
            log($"Keine Koordinaten erkannt: {entry.Key} / '{entry.Location}'");
        }

        tex.AppendLine();
        tex.AppendLine($"% ---- {entry.Key} ----");
        if (isShortEntry)
        {
            tex.AppendLine($"\\DiaryShortEntry{{{LatexText.Escape(longDate)}}}{{{LatexText.Escape(title)}}}{{{BuildMetaLine(entry)}}}{{{LatexText.Escape(toc)}}}");
            return;
        }
        tex.AppendLine($"\\DiaryEntryHead{{{LatexText.Escape(longDate)}}}{{{LatexText.Escape(title)}}}{{{BuildMetaLine(entry)}}}{{{(mapFile is null ? string.Empty : "images/" + mapFile)}}}{{{LatexText.Escape(toc)}}}");

        var selected = entry.Images.Where(i => i.Selected).OrderBy(i => i.Order).ToList();
        var hero = selected.FirstOrDefault(i => i.IsHero);

        // Datei je Foto erst schreiben, wenn die Druckbreite feststeht (Verkleinerung auf 450 ppi).
        string Place(SourceImage image, double printWidthMm)
        {
            report?.CheckResolution(entry, image.Item.FileName, image.PixelWidth, printWidthMm);
            return ImageExport.Write(image, imageDir, ImageExport.BaseName(entry.Key, image.Item), printWidthMm);
        }

        if (hero is not null && ImageExport.Read(hero, entry, log, report) is { } heroImage)
        {
            // DiaryHero: volle Satzbreite, höchstens 0,42 \textheight hoch.
            var widthMm = Math.Min(geometry.TextWidth, 0.42 * geometry.TextHeight * heroImage.Aspect);
            tex.AppendLine($"\\DiaryHero{{{Place(heroImage, widthMm)}}}{{{LatexText.Inline(hero.Caption ?? string.Empty)}}}");
        }

        tex.AppendLine(LatexText.Body(entry.EditedText, withInitial: true));

        var sources = new Dictionary<string, SourceImage>();
        var gallery = new List<GalleryPhoto>();
        foreach (var image in selected.Where(i => !ReferenceEquals(i, hero)))
        {
            if (ImageExport.Read(image, entry, log, report) is not { } source) continue;
            var id = ImageExport.BaseName(entry.Key, image);
            if (!sources.TryAdd(id, source)) continue; // dasselbe Foto doppelt ausgewählt
            gallery.Add(new GalleryPhoto(id, source.Aspect, LatexText.Inline(image.Caption ?? string.Empty), source.PixelWidth, image.FileName));
        }
        if (gallery.Count > 0)
            tex.Append(GalleryLayout.Render(gallery, (photo, fraction) => Place(sources[photo.File], fraction * geometry.TextWidth)));

        tex.AppendLine("\\DiaryEntryEnd");
    }
    public static bool IsEmpty(EntryModel entry)
        => string.IsNullOrWhiteSpace(entry.EditedText?.Replace(' ', ' ')) && !entry.Images.Any(i => i.Selected);

    private static string BuildMetaLine(EntryModel entry)
    {
        var parts = new List<string>();
        var place = !string.IsNullOrWhiteSpace(entry.PlaceName)
            ? entry.PlaceName
            : MapService.TryParseLatLon(entry.Location, out _, out _) ? string.Empty : entry.Location;
        if (!string.IsNullOrWhiteSpace(place))
            parts.Add($"\\DiaryPlace{{{LatexText.Escape(place)}}}");
        // Projekte aus v20 enthalten noch „🌤️ Wetter: …“.
        var weather = Regex.Replace(entry.Weather ?? string.Empty, @"^[^\p{L}\p{N}]*(Wetter\s*:\s*)?", string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(weather))
            parts.Add($"\\DiaryWeather{{{LatexText.Escape(weather)}}}");
        if (entry.Rating > 0)
            parts.Add($"\\DiaryRating{{{Math.Min(entry.Rating, 5)}}}");
        if (entry.Tags.Count > 0)
            parts.Add($"\\DiaryTags{{{LatexText.Escape(string.Join(", ", entry.Tags))}}}");
        return string.Join("\\DiaryMetaSep ", parts);
    }

    internal static string BuildMonthStats(IReadOnlyCollection<EntryModel> entries)
    {
        var photos = entries.Sum(e => e.Images.Count(i => i.Selected));
        var places = entries.Select(e => e.PlaceName).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Count();
        var parts = new List<string> { entries.Count == 1 ? "1 Eintrag" : $"{entries.Count} Einträge" };
        if (photos > 0) parts.Add(photos == 1 ? "1 Foto" : $"{photos} Fotos");
        if (places > 1) parts.Add($"{places} Orte");
        return string.Join(" · ", parts);
    }

    internal static string BuildDateRange(IReadOnlyList<EntryModel> entries)
    {
        var dates = entries.Where(e => e.Date.HasValue).Select(e => e.Date!.Value).ToList();
        if (dates.Count == 0) return string.Empty;
        var first = dates.Min();
        var last = dates.Max();
        if (first.Year == last.Year && first.Month == last.Month) return first.ToString("MMMM yyyy", German);
        if (first.Year == last.Year) return $"{first.ToString("MMMM", German)} – {last.ToString("MMMM yyyy", German)}";
        return $"{first.ToString("MMMM yyyy", German)} – {last.ToString("MMMM yyyy", German)}";
    }

    /// <summary>
    /// Satzspiegel des Buches: in der Druckversion aus dem Druckprofil (Bundsteg hängt von der
    /// Seitenzahl ab – ohne bekannte Seitenzahl wird die des letzten Laufs bzw. eine Schätzung verwendet).
    /// </summary>
    public static BookGeometry GeometryFor(BookProject project, int? pageCount = null)
        => project.PrintVersion
            ? PrintProfiles.Find(project.PrintProfileId).InteriorGeometry(pageCount ?? project.LastPageCount ?? EstimatedPageCount)
            : BookGeometry.Screen;

    /// <summary>Typischer Umfang eines Jahrbuchs; nur als Startwert, bis die echte Seitenzahl bekannt ist.</summary>
    private const int EstimatedPageCount = 300;

    /// <summary>Schreibt die Seitenmaße nach bookgeometry.tex (wird von book.tex eingebunden).</summary>
    public static void WriteGeometry(string outputDir, BookGeometry geometry)
        => File.WriteAllText(Path.Combine(outputDir, "bookgeometry.tex"),
            "% Seitenmaße aus DiariumBookStudio (Druckprofil) – wird bei jedem Export neu geschrieben\n" + geometry.ToLatex(),
            new UTF8Encoding(false));

    /// <summary>Kopiert eine Layoutdatei in den Ausgabeordner. Eine eigene Version im Projektordner hat Vorrang.</summary>
    public static void CopyStyle(BookProject project, string outputDir, string fileName = "diarybook.sty")
    {
        var custom = Path.Combine(project.ProjectRoot, fileName);
        var bundled = Path.Combine(AppContext.BaseDirectory, "Templates", fileName);
        var source = File.Exists(custom) ? custom : bundled;
        if (!File.Exists(source))
            throw new FileNotFoundException($"Layoutdatei {fileName} wurde nicht gefunden.", bundled);
        File.Copy(source, Path.Combine(outputDir, fileName), true);
    }
}