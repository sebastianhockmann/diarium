using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

/// <summary>
/// Bündelt Innenteil und Umschlag mit einer Druckinfo (Produkt, Maße, Schritte zum Hochladen)
/// in einem Ordner – zum manuellen Bestellen, z. B. eines Probeexemplars.
/// </summary>
public static class PrintPackage
{
    /// <summary>Verschiebt die beiden PDFs in einen neuen Paketordner und gibt dessen Pfad zurück.</summary>
    public static string Create(BookProject project, PrintProfile profile, int pageCount,
        string interiorPdf, string coverPdf, string outputDir, IEnumerable<string> notes)
    {
        var name = FileSafe(string.IsNullOrWhiteSpace(project.BookTitle) ? "Tagebuch" : project.BookTitle);
        var folder = Path.Combine(outputDir, $"Druckpaket_{name}_{DateTime.Now:yyyy-MM-dd_HHmm}");
        Directory.CreateDirectory(folder);

        var interiorName = $"{name}_Innenteil.pdf";
        var coverName = $"{name}_Umschlag.pdf";
        // Verschieben statt kopieren: Die PDFs sind oft über 100 MB groß.
        File.Move(interiorPdf, Path.Combine(folder, interiorName), overwrite: true);
        File.Move(coverPdf, Path.Combine(folder, coverName), overwrite: true);

        var (width, height, spine) = profile.CoverSize(pageCount);
        var info = new StringBuilder()
            .AppendLine($"Druckpaket „{project.BookTitle}“ – erstellt mit DiariumBookStudio am {DateTime.Now:dd.MM.yyyy HH:mm}")
            .AppendLine()
            .AppendLine($"Druckerei:   {profile.Provider}")
            .AppendLine($"Produkt:     {profile.Name}")
            .AppendLine($"Produktcode: {profile.PodPackageId}")
            .AppendLine()
            .AppendLine($"Innenteil:   {interiorName}")
            .AppendLine($"             {pageCount} Seiten, Endformat {Mm(profile.TrimWidth)} × {Mm(profile.TrimHeight)} mm,")
            .AppendLine($"             mit Beschnitt {Mm(profile.TrimWidth + 2 * profile.Bleed)} × {Mm(profile.TrimHeight + 2 * profile.Bleed)} mm")
            .AppendLine($"Umschlag:    {coverName}")
            .AppendLine($"             {Mm(width)} × {Mm(height)} mm mit Beschnitt, Buchrücken {Mm(spine)} mm")
            .AppendLine()
            .AppendLine("So wird bestellt (Bezeichnungen auf der Website können abweichen):")
            .AppendLine($"  1. {profile.OrderUrl} öffnen und anmelden, ein neues Druckbuch anlegen.")
            .AppendLine("     Ein Verkauf ist nicht nötig: Fragt die Website nach Vertrieb oder ISBN, das Buch privat lassen")
            .AppendLine("     bzw. ohne ISBN fortfahren – Exemplare lassen sich trotzdem bestellen.")
            .AppendLine("  2. Diese Produktoptionen wählen:");
        foreach (var option in profile.ProductOptions)
            info.AppendLine($"       - {option}");
        info.AppendLine($"  3. Innenteil hochladen: {interiorName}")
            .AppendLine($"  4. Umschlag als fertiges PDF hochladen (nicht den Umschlag-Designer verwenden): {coverName}")
            .AppendLine($"     Die Druckerei zeigt die erwarteten Umschlagmaße an – sie sollten {Mm(width)} × {Mm(height)} mm")
            .AppendLine($"     bzw. einen Buchrücken von {Mm(spine)} mm ergeben. Weicht das ab, bitte melden.")
            .AppendLine("  5. Vorschau prüfen und ein Exemplar bestellen.");

        var noteList = notes.ToList();
        if (noteList.Count > 0)
        {
            info.AppendLine().AppendLine("Hinweise:");
            foreach (var note in noteList)
                info.AppendLine($"  - {note}");
        }

        File.WriteAllText(Path.Combine(folder, "Druckinfo.txt"), info.ToString(), new UTF8Encoding(true));
        return folder;
    }

    private static string Mm(double value) => value.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Dateiname ohne Sonderzeichen: Umlaute umschreiben, alles andere durch _ ersetzen.</summary>
    private static string FileSafe(string text)
    {
        var replaced = text
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
            .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");
        var safe = Regex.Replace(replaced, @"[^A-Za-z0-9\-]+", "_").Trim('_');
        return safe.Length == 0 ? "Tagebuch" : safe.Length > 60 ? safe[..60] : safe;
    }
}
