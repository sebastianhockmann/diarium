using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

/// <summary>
/// Erzeugt cover.tex für den Buchumschlag (Rückseite, Rücken, Vorderseite als ein Blatt).
/// Die Rückenbreite hängt von der Seitenzahl des fertigen Innenteils ab – der Umschlag wird
/// deshalb erst nach dem Innenteil gesetzt. Das Layout steckt in diarycover.sty.
/// </summary>
public static class CoverExporter
{
    /// <summary>Anteil der Vorderseite, den das Foto oben einnimmt (siehe \dc@photob in diarycover.sty).</summary>
    private const double PhotoShare = 0.6;

    public static string Export(BookProject project, PrintProfile profile, int pageCount, string outputDir,
        PreflightReport? report = null)
    {
        LatexExporter.CopyStyle(project, outputDir, "diarycover.sty");
        var imageDir = Path.Combine(outputDir, "images");
        Directory.CreateDirectory(imageDir);

        var entries = project.BookEntries
            .Where(e => !(LatexExporter.IsEmpty(e) && string.IsNullOrWhiteSpace(e.Title)))
            .OrderBy(e => e.Date ?? DateTime.MaxValue)
            .ToList();
        var (width, height, spine) = profile.CoverSize(pageCount);

        string photoFile = string.Empty, fit = "w";
        if (ChooseCoverImage(project, profile, entries, report) is var (image, entry, source))
        {
            var (printWidth, byHeight) = PhotoPrintWidth(source, profile);
            fit = byHeight ? "h" : "w";
            report?.CheckResolution(entry, $"{image.FileName} (Umschlag)", source.PixelWidth, printWidth);
            photoFile = ImageExport.Write(source, imageDir, ImageExport.BaseName("cover", image), printWidth);
        }

        // Lulu: bei dünnen Büchern keine Schrift auf dem Rücken, sie würde auf die Vorderseite rutschen.
        var spineText = pageCount >= profile.MinPagesForSpineText ? LatexText.Escape(project.BookTitle) : string.Empty;
        // Schrift höchstens knapp die halbe Rückenbreite hoch, zwischen 6 und 12 pt.
        var spineFontSize = Math.Clamp(spine * 0.45 * 72 / 25.4, 6, 12);

        var tex = new StringBuilder();
        tex.AppendLine($"% Buchumschlag – automatisch erzeugt mit DiariumBookStudio, Layout siehe diarycover.sty");
        tex.AppendLine($"% {profile.Name}: {pageCount} Seiten, Rücken {spine:0.0} mm, Gesamtmaß {width:0.0} × {height:0.0} mm");
        tex.AppendLine($"\\def\\DiaryTrimWidth{{{BookGeometry.Mm(profile.TrimWidth)}}}");
        tex.AppendLine($"\\def\\DiaryTrimHeight{{{BookGeometry.Mm(profile.TrimHeight)}}}");
        tex.AppendLine($"\\def\\DiaryBleed{{{BookGeometry.Mm(profile.Bleed)}}}");
        tex.AppendLine($"\\def\\DiarySpine{{{BookGeometry.Mm(spine)}}}");
        tex.AppendLine($"\\def\\DiarySafety{{{BookGeometry.Mm(profile.SafetyMargin)}}}");
        tex.AppendLine($"\\def\\DiarySpineFontSize{{{spineFontSize.ToString("0.0", CultureInfo.InvariantCulture)}}}");
        tex.AppendLine("\\documentclass{article}");
        tex.AppendLine("\\usepackage{diarycover}");
        tex.AppendLine("\\begin{document}");
        tex.AppendLine($"\\DiaryCover{{{LatexText.Escape(project.BookTitle)}}}{{{LatexText.Escape(project.BookSubtitle)}}}" +
                       $"{{{LatexText.Escape(LatexExporter.BuildDateRange(entries))}}}{{{photoFile}}}{{{fit}}}" +
                       $"{{{LatexText.Escape(LatexExporter.BuildMonthStats(entries))}}}{{{spineText}}}");
        tex.AppendLine("\\end{document}");

        var texPath = Path.Combine(outputDir, "cover.tex");
        File.WriteAllText(texPath, tex.ToString(), new UTF8Encoding(false));
        return texPath;
    }

    /// <summary>
    /// Das Foto füllt die Fläche oben auf der Vorderseite (wird beschnitten): auf die Höhe skaliert,
    /// wenn es breiter ist als die Fläche, sonst auf die Breite. Liefert die Druckbreite in mm.
    /// </summary>
    private static (double PrintWidth, bool ByHeight) PhotoPrintWidth(SourceImage source, PrintProfile profile)
    {
        var boxWidth = profile.TrimWidth + profile.Bleed;
        var boxHeight = PhotoShare * profile.TrimHeight + profile.Bleed;
        var byHeight = source.Aspect > boxWidth / boxHeight;
        return (byHeight ? boxHeight * source.Aspect : boxWidth, byHeight);
    }

    /// <summary>Auflösung, ab der ein Foto automatisch als Umschlagfoto infrage kommt (Lulu-Empfehlung).</summary>
    private const double CoverMinPpi = 300;

    /// <summary>
    /// Gewähltes Umschlagfoto. Sonst automatisch: zuerst Titelbilder, dann alle ausgewählten Fotos –
    /// das erste, das auf dem Umschlag mindestens 300 ppi erreicht, sonst das schärfste.
    /// </summary>
    private static (ImageItem Image, EntryModel Entry, SourceImage Source)? ChooseCoverImage(BookProject project,
        PrintProfile profile, IReadOnlyList<EntryModel> entries, PreflightReport? report)
    {
        if (!string.IsNullOrEmpty(project.CoverImage))
        {
            foreach (var entry in project.Entries)
                foreach (var image in entry.Images)
                    if (image.RelativePath == project.CoverImage)
                        return ImageExport.Read(image, entry, _ => { }, report) is { } chosen ? (image, entry, chosen) : null;
            report?.Add(PreflightSeverity.Warnung, null, "Das gewählte Umschlagfoto ist nicht mehr im Projekt – es wird automatisch eines gewählt.");
        }

        var candidates = entries
            .SelectMany(e => e.Images.Where(i => i.Selected).OrderBy(i => i.Order).Select(i => (Image: i, Entry: e)))
            .OrderBy(c => c.Image.IsHero ? 0 : 1); // stabil: Reihenfolge im Buch bleibt erhalten
        (ImageItem Image, EntryModel Entry, SourceImage Source)? best = null;
        double bestPpi = 0;
        foreach (var (image, entry) in candidates)
        {
            // Nur der Dateikopf wird gelesen; fehlende Fotos meldet bereits der Innenteil.
            if (ImageExport.Read(image, entry, _ => { }, null) is not { } source) continue;
            var ppi = source.PixelWidth / (PhotoPrintWidth(source, profile).PrintWidth / 25.4);
            if (ppi > bestPpi) (best, bestPpi) = ((image, entry, source), ppi);
            if (ppi >= CoverMinPpi) break;
        }
        if (best is { } result)
            report?.Add(PreflightSeverity.Hinweis, result.Entry,
                $"Kein Umschlagfoto gewählt – verwendet wird {result.Image.FileName} ({bestPpi:0} ppi). Anderes Foto: im Eintrag „Als Umschlagfoto“ wählen.");
        return best;
    }
}