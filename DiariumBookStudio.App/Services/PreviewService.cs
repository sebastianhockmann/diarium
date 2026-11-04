using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

/// <param name="Number">Seitenzahl in der Vorschau (beginnt bei 1, ungerade = rechte Seite).</param>
/// <param name="StartingEntries">Einträge, die auf dieser Seite beginnen.</param>
/// <param name="ContinuedEntry">Eintrag, der von der vorigen Seite weiterläuft (oder null).</param>
public sealed record PreviewPage(int Number, string ImagePath, IReadOnlyList<EntryModel> StartingEntries, EntryModel? ContinuedEntry);

/// <param name="ContentPages">Seiten mit Einträgen (ohne Monatsseite).</param>
public sealed record PreviewResult(string Title, bool WholeMonth, IReadOnlyList<PreviewPage> Pages,
    int EntryCount, int PhotoCount, int ContentPages, string PdfPath);

/// <summary>
/// Setzt einen Eintrag oder einen ganzen Monat mit demselben Satzspiegel wie das Buch und rendert
/// die Seiten als Bilder – so ist in Sekunden zu sehen, wie Text und Fotos fallen.
/// </summary>
public static class PreviewService
{
    /// <summary>Höhe der gerenderten Seiten in Pixeln (die Anzeige skaliert sie).</summary>
    private const double RenderHeightPx = 1000;

    public static async Task<PreviewResult> CreateAsync(BookProject project, EntryModel entry, bool wholeMonth,
        AppSettings settings, IProgress<string>? progress = null)
    {
        var outputDir = project.OutputFolder;
        Directory.CreateDirectory(outputDir);
        var texPath = Path.Combine(outputDir, "preview.tex");

        Func<EntryModel, bool> filter = wholeMonth && entry.Date is { } date
            ? e => e.Date?.Year == date.Year && e.Date?.Month == date.Month
            : e => ReferenceEquals(e, entry);
        progress?.Report("Vorschau: Fotos und Karten vorbereiten...");
        await LatexExporter.ExportAsync(project, texPath, progress, null, new PreviewOptions(filter, MonthPages: wholeMonth));

        progress?.Report("Vorschau: LuaLaTeX setzt die Seiten...");
        await LatexRunner.RunAsync(settings.LuaLatexCommand, texPath, "preview");
        var pdfPath = Path.Combine(outputDir, "preview.pdf");

        progress?.Report("Vorschau: Seiten werden gerendert...");
        var profile = PrintProfiles.Find(project.PrintProfileId);
        var dpi = (int)Math.Round(RenderHeightPx / (profile.TrimHeight / 25.4));
        var images = await LatexRunner.RenderPagesAsync(settings.PdfToPpmCommand, pdfPath, Path.Combine(outputDir, "preview_pages"), dpi);

        var byKey = project.Entries.Where(e => !string.IsNullOrEmpty(e.Key)).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.First());
        var marks = LatexRunner.EntryPages(Path.Combine(outputDir, "preview.log"))
            .Where(m => byKey.ContainsKey(m.Key))
            .Select(m => (m.Page, Entry: byKey[m.Key]))
            .ToList();

        var pages = new List<PreviewPage>();
        EntryModel? running = null;
        for (var i = 0; i < images.Count; i++)
        {
            var number = i + 1;
            var starting = marks.Where(m => m.Page == number).Select(m => m.Entry).ToList();
            pages.Add(new PreviewPage(number, images[i], starting, running));
            if (starting.Count > 0) running = starting[^1];
        }

        var entries = marks.Select(m => m.Entry).Distinct().ToList();
        var contentPages = Math.Max(1, images.Count - (wholeMonth ? 1 : 0));
        var title = wholeMonth && entry.Date is { } d
            ? d.ToString("MMMM yyyy", new System.Globalization.CultureInfo("de-DE"))
            : entry.DisplayTitle;
        return new PreviewResult(title, wholeMonth, pages, entries.Count,
            entries.Sum(e => e.Images.Count(i => i.Selected)), contentPages, pdfPath);
    }
}
