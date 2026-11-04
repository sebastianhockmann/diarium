using System.Collections.Generic;
using System.Linq;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

public enum PreflightSeverity
{
    Fehler,
    Warnung,
    Hinweis
}

/// <param name="Entry">Betroffener Eintrag, null bei Hinweisen zum ganzen Buch.</param>
public sealed record PreflightIssue(PreflightSeverity Severity, EntryModel? Entry, string Message)
{
    public string SeverityText => Severity.ToString();
    public string EntryText => Entry?.DisplayTitle ?? "Buch";
}

/// <summary>
/// Sammelt beim Export Probleme, die das gedruckte Buch betreffen: fehlende oder defekte Bilder,
/// zu geringe Auflösung in der tatsächlichen Druckgröße, ungeprüfte Einträge.
/// </summary>
public sealed class PreflightReport
{
    /// <summary>Satzspiegel aus diarybook.sty (A5 148 × 210 mm, innen 19, außen 15, oben 18, unten 20 mm).</summary>
    public const double TextWidthMm = 114;
    public const double TextHeightMm = 172;

    /// <summary>Unter 200 dpi werden Fotos im Druck sichtbar weicher, unter 120 dpi deutlich unscharf.</summary>
    public const double MinDpi = 200;
    public const double PoorDpi = 120;

    private readonly List<PreflightIssue> _issues = new();

    public IReadOnlyList<PreflightIssue> Issues => _issues;

    public bool HasIssues => _issues.Count > 0;

    public void Add(PreflightSeverity severity, EntryModel? entry, string message)
        => _issues.Add(new PreflightIssue(severity, entry, message));

    /// <summary>Prüft die Auflösung eines Fotos, das <paramref name="printWidthMm"/> breit gedruckt wird.</summary>
    public void CheckResolution(EntryModel entry, string fileName, double pixelWidth, double printWidthMm)
    {
        if (printWidthMm <= 0) return;
        var dpi = pixelWidth / (printWidthMm / 25.4);
        if (dpi >= MinDpi) return;
        var severity = dpi < PoorDpi ? PreflightSeverity.Warnung : PreflightSeverity.Hinweis;
        var quality = dpi < PoorDpi ? "wird deutlich unscharf" : "wird etwas weicher";
        Add(severity, entry, $"Foto {fileName} {quality}: {dpi:0} dpi bei {printWidthMm:0} mm Druckbreite (empfohlen ab {MinDpi:0} dpi).");
    }

    public string Summary
    {
        get
        {
            if (!HasIssues) return "Keine Auffälligkeiten.";
            var parts = Enum.GetValues<PreflightSeverity>()
                .Select(s => (Severity: s, Count: _issues.Count(i => i.Severity == s)))
                .Where(x => x.Count > 0)
                .Select(x => $"{x.Count} × {x.Severity}");
            return string.Join(", ", parts);
        }
    }
}
