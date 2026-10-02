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
    public static BookProject ImportZip(string zipPath, string projectRoot)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP-Datei nicht gefunden.", zipPath);

        Directory.CreateDirectory(projectRoot);
        var sourceFolder = Path.Combine(projectRoot, "source");
        if (Directory.Exists(sourceFolder))
            Directory.Delete(sourceFolder, true);
        Directory.CreateDirectory(sourceFolder);

        ZipFile.ExtractToDirectory(zipPath, sourceFolder);
        var htmlFile = Directory.GetFiles(sourceFolder, "*.html", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException("In der ZIP-Datei wurde keine HTML-Datei gefunden.");

        var html = File.ReadAllText(htmlFile);
        var entries = ParseEntries(html, Path.GetDirectoryName(htmlFile) ?? sourceFolder);

        var project = new BookProject
        {
            ProjectRoot = projectRoot,
            SourceZipPath = zipPath,
            ExtractedFolder = sourceFolder,
            HtmlFile = htmlFile,
            BookTitle = "Tagebuch Mai 2026",
            Entries = entries
        };

        project.Save();
        return project;
    }

    private static ObservableCollection<EntryModel> ParseEntries(string html, string htmlFolder)
    {
        var result = new ObservableCollection<EntryModel>();
        var marker = "<div class=\"entry\">";
        var parts = html.Split(marker, StringSplitOptions.None).Skip(1).ToList();

        for (var i = 0; i < parts.Count; i++)
        {
            var raw = parts[i];
            var nextEntryIndex = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (nextEntryIndex >= 0) raw = raw[..nextEntryIndex];

            var entry = new EntryModel
            {
                DateText = Clean(Extract(raw, "<div class=\"date\">", "</div>")),
                Title = Clean(Extract(raw, "<span class=\"title\">", "</span>")),
                OriginalText = Clean(ExtractContent(raw)),
                Location = Clean(Extract(raw, "<div class=\"location\">", "</div>")),
                Weather = Clean(Extract(raw, "<div class=\"weather\">", "</div>"))
            };
            entry.Date = ParseGermanDate(entry.DateText);
            entry.EditedText = entry.OriginalText;

            var imageMatches = Regex.Matches(raw, "<img[^>]+src=\"(?<src>[^\"]+)\"", RegexOptions.IgnoreCase);
            var order = 1;
            foreach (Match match in imageMatches)
            {
                var src = WebUtility.HtmlDecode(match.Groups["src"].Value).Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(htmlFolder, src));
                entry.Images.Add(new ImageItem
                {
                    RelativePath = WebUtility.HtmlDecode(match.Groups["src"].Value),
                    FullPath = fullPath,
                    Order = order++,
                    Selected = order <= 7,
                    Role = order == 2 ? "Hauptbild" : "Galerie"
                });
            }

            result.Add(entry);
        }

        return result;
    }

    private static string ExtractContent(string html)
    {
        var contentStart = html.IndexOf("<div class=\"content\">", StringComparison.OrdinalIgnoreCase);
        if (contentStart < 0) return string.Empty;
        contentStart += "<div class=\"content\">".Length;
        var photosStart = html.IndexOf("<div class=\"photos\">", contentStart, StringComparison.OrdinalIgnoreCase);
        var end = photosStart > contentStart ? photosStart : html.IndexOf("</div>", contentStart, StringComparison.OrdinalIgnoreCase);
        if (end < 0 || end <= contentStart) return string.Empty;
        return html[contentStart..end];
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

    private static DateTime? ParseGermanDate(string text)
    {
        var match = Regex.Match(text, @"(?<day>\d{1,2})\.\s*(?<month>[A-Za-zäöüÄÖÜ]+)\s*(?<year>\d{4})");
        if (!match.Success) return null;

        var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Januar"] = 1, ["Februar"] = 2, ["März"] = 3, ["Maerz"] = 3,
            ["April"] = 4, ["Mai"] = 5, ["Juni"] = 6, ["Juli"] = 7,
            ["August"] = 8, ["September"] = 9, ["Oktober"] = 10,
            ["November"] = 11, ["Dezember"] = 12
        };

        if (!int.TryParse(match.Groups["day"].Value, out var day)) return null;
        if (!int.TryParse(match.Groups["year"].Value, out var year)) return null;
        if (!months.TryGetValue(match.Groups["month"].Value, out var month)) return null;
        return new DateTime(year, month, day);
    }
}
