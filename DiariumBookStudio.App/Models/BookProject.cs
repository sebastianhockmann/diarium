using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace DiariumBookStudio.Models;

public class BookProject
{
    public string ProjectRoot { get; set; } = string.Empty;
    public string SourceZipPath { get; set; } = string.Empty;
    public string ExtractedFolder { get; set; } = string.Empty;
    public string HtmlFile { get; set; } = string.Empty;
    public string BookTitle { get; set; } = "Mein Tagebuch";
    public ObservableCollection<EntryModel> Entries { get; set; } = new();

    [JsonIgnore]
    public string ProjectFile => Path.Combine(ProjectRoot, "diarium-book-project.json");

    public void Save()
    {
        Directory.CreateDirectory(ProjectRoot);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(ProjectFile, json);
    }

    public static BookProject Load(string projectFile)
    {
        var json = File.ReadAllText(projectFile);
        var project = JsonSerializer.Deserialize<BookProject>(json) ?? new BookProject();
        project.ProjectRoot = Path.GetDirectoryName(projectFile) ?? string.Empty;
        return project;
    }
}
