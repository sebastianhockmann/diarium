using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using DiariumBookStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiariumBookStudio.Services;

public static class DiariumImporter
{
    /// <summary>
    /// Importiert eine Diarium-HTML-ZIP. Existiert im Projektordner bereits ein Projekt,
    /// werden dessen Bearbeitungen (Text, Prüfstatus, Bildauswahl, Bildunterschriften, Ortsname)
    /// in die neu importierten Einträge übernommen.
    /// </summary>
    public static BookProject ImportZip(string zipPath, string projectRoot)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP-Datei nicht gefunden.", zipPath);

        Directory.CreateDirectory(projectRoot);
        var projectFile = Path.Combine(projectRoot, "diarium-book-project.json");
        var existing = File.Exists(projectFile) ? BookProject.Load(projectFile) : null;

        var sourceFolder = Path.Combine(projectRoot, "source");
        if (Directory.Exists(sourceFolder))
            Directory.Delete(sourceFolder, true);
        Directory.CreateDirectory(sourceFolder);

        ZipFile.ExtractToDirectory(zipPath, sourceFolder);
        var htmlFile = Directory.GetFiles(sourceFolder, "*.html", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException("In der ZIP-Datei wurde keine HTML-Datei gefunden.");

        var html = File.ReadAllText(htmlFile);
        var entries = ParseEntries(html);
        BookProject.AssignKeys(entries);

        var project = new BookProject
        {
            ProjectRoot = projectRoot,
            SourceZipPath = zipPath,
            HtmlFile = htmlFile,
            BookTitle = existing?.BookTitle ?? BuildDefaultTitle(entries),
            BookSubtitle = existing?.BookSubtitle ?? string.Empty,
            PrintVersion = existing?.PrintVersion ?? false,
            Entries = entries
        };
        project.ResolvePaths();

        if (existing is not null)
            MergeEdits(existing, project);

        project.Save();
        return project;
    }

    private static void MergeEdits(BookProject existing, BookProject imported)
    {
        var oldEntries = existing.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Key))
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var entry in imported.Entries)
        {
            if (!oldEntries.TryGetValue(entry.Key, out var old)) continue;

            // Nur übernehmen, wenn tatsächlich in der App bearbeitet wurde.
            // Sonst gewinnt der (eventuell in Diarium geänderte) neue Text.
            if (old.EditedText != old.OriginalText)
                entry.EditedText = old.EditedText;
            entry.Done = old.Done;
            entry.PlaceName = old.PlaceName;

            var oldImages = old.Images.ToDictionary(i => i.RelativePath, StringComparer.OrdinalIgnoreCase);
            var maxOrder = old.Images.Count == 0 ? 0 : old.Images.Max(i => i.Order);
            foreach (var image in entry.Images)
            {
                if (oldImages.TryGetValue(image.RelativePath, out var oldImage))
                {
                    image.Selected = oldImage.Selected;
                    image.IsHero = oldImage.IsHero;
                    image.Caption = oldImage.Caption;
                    image.Order = oldImage.Order;
                }
                else
                {
                    // Neu hinzugekommene Bilder hinten anhängen, aber nicht automatisch auswählen.
                    image.Order = ++maxOrder;
                    image.Selected = false;
                }
            }
        }
    }

    private static string BuildDefaultTitle(IList<EntryModel> entries)
    {
        var dates = entries.Where(e => e.Date.HasValue).Select(e => e.Date!.Value).OrderBy(d => d).ToList();
        if (dates.Count == 0) return "Mein Tagebuch";

        var de = new CultureInfo("de-DE");
        var first = dates.First();
        var last = dates.Last();
        if (first.Year == last.Year && first.Month == last.Month)
            return $"Tagebuch {first.ToString("MMMM yyyy", de)}";
        if (first.Year == last.Year)
            return $"Tagebuch {first.ToString("MMMM", de)} – {last.ToString("MMMM yyyy", de)}";
        return $"Tagebuch {first.ToString("MMMM yyyy", de)} – {last.ToString("MMMM yyyy", de)}";
    }

    private static ObservableCollection<EntryModel> ParseEntries(string html)
    {
        var result = new ObservableCollection<EntryModel>();
        var marker = "<div class=\"entry\">";
        var parts = html.Split(marker, StringSplitOptions.None).Skip(1).ToList();

        foreach (var raw in parts)
        {
            var entry = new EntryModel
            {
                DateText = StripSymbols(Clean(Extract(raw, "<div class=\"date\">", "</div>"))),
                Title = Clean(Extract(raw, "<span class=\"title\">", "</span>")),
                OriginalText = ConvertContent(ExtractContent(raw)),
                Location = StripSymbols(Clean(Extract(raw, "<div class=\"location\">", "</div>"))),
                Weather = CleanWeather(Clean(Extract(raw, "<div class=\"weather\">", "</div>"))),
                Rating = Clean(Extract(raw, "<span class=\"rating\">", "</span>")).Count(c => c == '★'),
                Tags = ParseTags(Clean(Extract(raw, "<div class=\"tags\">", "</div>")))
            };
            entry.EditedText = entry.OriginalText;

            var imageMatches = Regex.Matches(raw, "<img[^>]+src=\"(?<src>[^\"]+)\"", RegexOptions.IgnoreCase);
            var order = 0;
            foreach (Match match in imageMatches)
            {
                order++;
                entry.Images.Add(new ImageItem
                {
                    RelativePath = WebUtility.HtmlDecode(match.Groups["src"].Value),
                    Order = order,
                    Selected = order <= 6
                });
            }

            entry.Date = ParseDate(entry.DateText) ?? ParseDateFromMediaFolder(entry.Images.FirstOrDefault()?.RelativePath);
            result.Add(entry);
        }

        return result;
    }

    private static string ExtractContent(string html)
    {
        var contentStart = html.IndexOf("<div class=\"content\">", StringComparison.OrdinalIgnoreCase);
        if (contentStart < 0) return string.Empty;
        contentStart += "<div class=\"content\">".Length;

        // Das Content-Div kann verschachtelte Divs enthalten. Deshalb bis zum passenden </div> zählen.
        var depth = 1;
        var pos = contentStart;
        var tag = new Regex("<(/?)div\\b[^>]*>", RegexOptions.IgnoreCase);
        while (depth > 0)
        {
            var match = tag.Match(html, pos);
            if (!match.Success) return html[contentStart..];
            depth += match.Groups[1].Value == "/" ? -1 : 1;
            if (depth == 0) return html[contentStart..match.Index];
            pos = match.Index + match.Length;
        }
        return string.Empty;
    }

    private static string Extract(string html, string startMarker, string endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return string.Empty;
        start += startMarker.Length;
        var end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0 || end <= start) return string.Empty;
        return html[start..end];
    }

    private static string Clean(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, "[ \\t]+", " ").Trim();
    }

    /// <summary>
    /// Wandelt den HTML-Inhalt in Text mit einfacher Markdown-Auszeichnung um:
    /// **fett**, *kursiv*, „- “ für Listenpunkte. So bleibt der Text im Editor lesbar
    /// und die Formatierung geht trotzdem nicht verloren.
    /// </summary>
    private static string ConvertContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "[\\r\\n]+", " ");
        text = Regex.Replace(text, "</?(strong|b)\\b[^>]*>", "**", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</?(em|i)\\b[^>]*>", "*", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<h[1-6]\\b[^>]*>", "\n\n**", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</h[1-6]>", "**\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<li\\b[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</(ul|ol)>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</(p|div)>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        text = Regex.Replace(text, "\\*\\*\\s*\\*\\*", string.Empty);
        text = Regex.Replace(text, "[ \\t]+", " ");
        text = Regex.Replace(text, " *\n *", "\n");
        text = Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>Entfernt Emoji und Symbole wie 🗓️ oder 📍 am Anfang von Metadaten.</summary>
    private static string StripSymbols(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // Nur Emoji-Bereiche entfernen – nicht alle Symbole, sonst verschwindet z. B. „°“ aus „21 °C“.
        var cleaned = Regex.Replace(text, "[\\p{Cs}\\u2600-\\u27BF\\u2B00-\\u2BFF\\uFE0F\\u200D]", string.Empty);
        return Regex.Replace(cleaned, "[ \\t]+", " ").Trim();
    }

    private static string CleanWeather(string text)
    {
        var weather = StripSymbols(text);
        return Regex.Replace(weather, @"^(Wetter\s*:\s*|Weather\s*:\s*)+", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static List<string> ParseTags(string text)
    {
        return StripSymbols(text)
            .Split(new[] { ',', '#', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();
    }

    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Januar"] = 1, ["Jänner"] = 1, ["Februar"] = 2, ["März"] = 3, ["Maerz"] = 3,
        ["April"] = 4, ["Mai"] = 5, ["Juni"] = 6, ["Juli"] = 7, ["August"] = 8,
        ["September"] = 9, ["Oktober"] = 10, ["November"] = 11, ["Dezember"] = 12,
        ["January"] = 1, ["February"] = 2, ["March"] = 3, ["May"] = 5, ["June"] = 6,
        ["July"] = 7, ["October"] = 10, ["December"] = 12
    };

    private static DateTime? ParseDate(string text)
    {
        // Deutsch: „Freitag, 1. Mai 2026“
        var match = Regex.Match(text, @"(?<day>\d{1,2})\.\s*(?<month>\p{L}+)\s*(?<year>\d{4})");
        if (!match.Success)
        {
            // Englisch: „Friday, May 1, 2026“
            match = Regex.Match(text, @"(?<month>\p{L}+)\s+(?<day>\d{1,2}),?\s+(?<year>\d{4})");
        }
        if (!match.Success) return null;

        if (!int.TryParse(match.Groups["day"].Value, out var day)) return null;
        if (!int.TryParse(match.Groups["year"].Value, out var year)) return null;
        if (!MonthNames.TryGetValue(match.Groups["month"].Value, out var month)) return null;
        try { return new DateTime(year, month, day); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Diarium legt Medien in Ordnern wie „media/2026-05-01_175041570/“ ab.</summary>
    private static DateTime? ParseDateFromMediaFolder(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        var match = Regex.Match(relativePath, @"(?<date>\d{4}-\d{2}-\d{2})_\d+");
        return match.Success && DateTime.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
