using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;

namespace DiariumBookStudio.Models;

public class BookProject
{
    public string ProjectRoot { get; set; } = string.Empty;
    public string SourceZipPath { get; set; } = string.Empty;
    public string ExtractedFolder { get; set; } = string.Empty;
    public string HtmlFile { get; set; } = string.Empty;
    public string BookTitle { get; set; } = "Mein Tagebuch";
    public string BookSubtitle { get; set; } = string.Empty;

    /// <summary>Druckversion: 3 mm Beschnittzugabe, TrimBox/BleedBox, Seitenzahl auf Vielfaches von 4.</summary>
    public bool PrintVersion { get; set; }

    public ObservableCollection<EntryModel> Entries { get; set; } = new();

    [JsonIgnore]
    public string ProjectFile => Path.Combine(ProjectRoot, "diarium-book-project.json");

    [JsonIgnore]
    public string OutputFolder => Path.Combine(ProjectRoot, "output");

    public void Save()
    {
        Directory.CreateDirectory(ProjectRoot);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(ProjectFile, json);
    }

    public static BookProject Load(string projectFile)
    {
        var json = File.ReadAllText(projectFile);
        var project = JsonSerializer.Deserialize<BookProject>(json) ?? new BookProject();
        project.ProjectRoot = Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
        project.ResolvePaths();
        if (project.Entries.Any(e => string.IsNullOrWhiteSpace(e.Key)))
            AssignKeys(project.Entries);
        return project;
    }

    /// <summary>
    /// Berechnet alle absoluten Pfade relativ zum Projektordner neu.
    /// Dadurch bleibt ein Projekt gültig, wenn der Ordner verschoben wird.
    /// </summary>
    public void ResolvePaths()
    {
        ExtractedFolder = Path.Combine(ProjectRoot, "source");
        if (!File.Exists(HtmlFile) && Directory.Exists(ExtractedFolder))
        {
            HtmlFile = Directory.GetFiles(ExtractedFolder, "*.html", SearchOption.AllDirectories).FirstOrDefault()
                       ?? HtmlFile;
        }

        var htmlFolder = Path.GetDirectoryName(HtmlFile) ?? ExtractedFolder;
        foreach (var image in Entries.SelectMany(e => e.Images))
        {
            image.FullPath = Path.GetFullPath(Path.Combine(htmlFolder, image.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    /// <summary>Vergibt Schlüssel der Form „2026-05-01-1“ (Datum + laufende Nummer am Tag).</summary>
    public static void AssignKeys(IEnumerable<EntryModel> entries)
    {
        var counter = new Dictionary<string, int>();
        foreach (var entry in entries)
        {
            var day = entry.Date?.ToString("yyyy-MM-dd") ?? "ohne-datum";
            counter[day] = counter.TryGetValue(day, out var n) ? n + 1 : 1;
            entry.Key = $"{day}-{counter[day]}";
        }
    }
}
