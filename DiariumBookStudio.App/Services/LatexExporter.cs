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
using System.Windows.Media.Imaging;

namespace DiariumBookStudio.Services;

/// <summary>
/// Erzeugt book.tex. Das Layout steckt in diarybook.sty (Templates-Ordner bzw.
/// eigene Version im Projektordner); hier werden nur Daten und Fotolayout geschrieben.
/// </summary>
public static class LatexExporter
{
    private static readonly CultureInfo German = new("de-DE");
    private static readonly HashSet<string> LatexImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

    /// <summary>
    /// Führt den Export auf einem eigenen STA-Thread aus (WPF-Rendering der Karten braucht STA),
    /// damit die Oberfläche während Kartendownloads nicht einfriert.
    /// </summary>
    public static Task<string> ExportAsync(BookProject project, string? outputTexPath = null, IProgress<string>? progress = null)
    {
        var tcs = new TaskCompletionSource<string>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(Export(project, outputTexPath, progress)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    public static string Export(BookProject project, string? outputTexPath = null, IProgress<string>? progress = null)
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

        var tex = new StringBuilder();
        tex.AppendLine("% Automatisch erzeugt mit DiariumBookStudio – Layout siehe diarybook.sty");
        tex.AppendLine("% Kompilieren: lualatex book.tex (zweimal, wegen Inhaltsverzeichnis)");
        tex.AppendLine("\\documentclass[11pt,twoside,openany]{scrbook}");
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
                AppendEntry(monthTex, entry, imageDir, maps, Log);
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

    private static void AppendEntry(StringBuilder tex, EntryModel entry, string imageDir, MapService maps, Action<string> log)
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
        var index = 1;

        if (hero is not null)
        {
            var heroPhoto = PrepareImage(hero, entry, imageDir, index++, log);
            if (heroPhoto is not null)
                tex.AppendLine($"\\DiaryHero{{{heroPhoto.File}}}{{{heroPhoto.Caption}}}");
        }

        tex.AppendLine(LatexText.Body(entry.EditedText, withInitial: true));

        var gallery = selected
            .Where(i => !ReferenceEquals(i, hero))
            .Select(i => PrepareImage(i, entry, imageDir, index++, log))
            .OfType<GalleryPhoto>()
            .ToList();
        if (gallery.Count > 0)
            tex.Append(GalleryLayout.Render(gallery));

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

    private static string BuildMonthStats(IReadOnlyCollection<EntryModel> entries)
    {
        var photos = entries.Sum(e => e.Images.Count(i => i.Selected));
        var places = entries.Select(e => e.PlaceName).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Count();
        var parts = new List<string> { entries.Count == 1 ? "1 Eintrag" : $"{entries.Count} Einträge" };
        if (photos > 0) parts.Add(photos == 1 ? "1 Foto" : $"{photos} Fotos");
        if (places > 1) parts.Add($"{places} Orte");
        return string.Join(" · ", parts);
    }

    private static string BuildDateRange(IReadOnlyList<EntryModel> entries)
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
    /// Kopiert ein Bild in den Ausgabeordner (eindeutiger Name pro Eintrag) und liest das Seitenverhältnis.
    /// Bilder mit EXIF-Drehung oder Formaten, die LaTeX nicht kennt, werden als JPEG neu geschrieben.
    /// </summary>
    private static GalleryPhoto? PrepareImage(ImageItem image, EntryModel entry, string imageDir, int index, Action<string> log)
    {
        if (!File.Exists(image.FullPath))
        {
            log($"Bild fehlt: {image.FullPath}");
            return null;
        }

        try
        {
            BitmapFrame frame;
            using (var stream = File.OpenRead(image.FullPath))
            {
                frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            }

            var rotation = ReadExifRotation(frame);
            var ext = Path.GetExtension(image.FullPath).ToLowerInvariant();
            var needsReencode = rotation != 0 || !LatexImageExtensions.Contains(ext);
            var fileName = $"{entry.Key}_{index:00}{(needsReencode ? ".jpg" : ext)}";
            var target = Path.Combine(imageDir, fileName);

            if (needsReencode)
            {
                BitmapSource source = frame;
                if (rotation != 0)
                {
                    source = new TransformedBitmap(frame, new System.Windows.Media.RotateTransform(rotation));
                }
                var encoder = new JpegBitmapEncoder { QualityLevel = 93 };
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var output = File.Create(target);
                encoder.Save(output);
            }
            else if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(image.FullPath).Length)
            {
                File.Copy(image.FullPath, target, true);
            }

            var swap = rotation is 90 or 270;
            double width = swap ? frame.PixelHeight : frame.PixelWidth;
            double height = swap ? frame.PixelWidth : frame.PixelHeight;
            return new GalleryPhoto("images/" + fileName, width / height, LatexText.Inline(image.Caption ?? string.Empty));
        }
        catch (Exception ex)
        {
            log($"Bild konnte nicht verarbeitet werden: {image.FullPath}: {ex.Message}");
            return null;
        }
    }

    private static int ReadExifRotation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
            {
                return orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
            }
        }
        catch (NotSupportedException) { }
        return 0;
    }

    /// <summary>Kopiert diarybook.sty in den Ausgabeordner. Eine eigene Version im Projektordner hat Vorrang.</summary>
    private static void CopyStyle(BookProject project, string outputDir)
    {
        var custom = Path.Combine(project.ProjectRoot, "diarybook.sty");
        var bundled = Path.Combine(AppContext.BaseDirectory, "Templates", "diarybook.sty");
        var source = File.Exists(custom) ? custom : bundled;
        if (!File.Exists(source))
            throw new FileNotFoundException("Layoutdatei diarybook.sty wurde nicht gefunden.", bundled);
        File.Copy(source, Path.Combine(outputDir, "diarybook.sty"), true);
    }
}
