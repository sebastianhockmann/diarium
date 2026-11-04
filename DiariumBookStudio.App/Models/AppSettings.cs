using System;
using System.IO;
using System.Text.Json;

namespace DiariumBookStudio.Models;

/// <summary>
/// Einstellungen für die ganze App (nicht pro Projekt), gespeichert unter
/// %APPDATA%\DiariumBookStudio\settings.json.
/// </summary>
public class AppSettings
{
    /// <summary>Hier legt der Import neue Projekte an (ein Unterordner pro Diarium-Export).</summary>
    public string ProjectsFolder { get; set; } = DefaultProjectsFolder;

    /// <summary>Ordner für Druckpakete. Leer: im Ausgabeordner des Projekts.</summary>
    public string PrintPackageFolder { get; set; } = string.Empty;

    /// <summary>Pfad zu lualatex.exe. Leer: aus dem Suchpfad (PATH), wie von MiKTeX eingerichtet.</summary>
    public string LuaLatexPath { get; set; } = string.Empty;

    /// <summary>Druckprodukt für neue Projekte.</summary>
    public string DefaultPrintProfileId { get; set; } = Services.PrintProfiles.Default.Id;

    /// <summary>Höchstzahl Fotos je Eintrag für die automatische Auswahl in neuen Projekten.</summary>
    public int DefaultMaxPhotosPerEntry { get; set; } = 6;

    /// <summary>Beim Start das zuletzt geöffnete Projekt wieder öffnen.</summary>
    public bool OpenLastProject { get; set; } = true;

    public string LastProjectFile { get; set; } = string.Empty;

    public static string DefaultProjectsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DiariumBookStudio");

    private static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiariumBookStudio", "settings.json");

    /// <summary>Programm für LuaLaTeX: eingestellter Pfad oder „lualatex“ aus dem Suchpfad.</summary>
    public string LuaLatexCommand => string.IsNullOrWhiteSpace(LuaLatexPath) ? "lualatex" : LuaLatexPath;

    /// <summary>pdftoppm (für die Vorschau) liegt bei MiKTeX und TeX Live neben lualatex.</summary>
    public string PdfToPpmCommand
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LuaLatexPath)) return "pdftoppm";
            var candidate = Path.Combine(Path.GetDirectoryName(LuaLatexPath) ?? string.Empty, "pdftoppm.exe");
            return File.Exists(candidate) ? candidate : "pdftoppm";
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Defekte Einstellungen: mit Voreinstellungen weiterarbeiten.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
