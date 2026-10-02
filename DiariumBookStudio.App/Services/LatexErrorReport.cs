using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiariumBookStudio.Services;

/// <summary>
/// Übersetzt die Ausgabe eines fehlgeschlagenen LuaLaTeX-Laufs in eine verständliche Meldung:
/// eigentliche Fehlerzeile, betroffener Tagebucheintrag und – für bekannte Fälle – ein Hinweis.
/// </summary>
public static class LatexErrorReport
{
    public static string Describe(string output, string texPath, string logPath)
    {
        var lines = output.Replace("\r\n", "\n").Split('\n');
        var errorIndex = Array.FindIndex(lines, l => l.StartsWith("! ") && !l.Contains("==> Fatal error"));
        var error = errorIndex >= 0 ? lines[errorIndex][2..].Trim() : null;

        // Fehlermeldungen können über mehrere Zeilen umbrochen sein (79 Zeichen pro Zeile).
        if (errorIndex >= 0 && lines[errorIndex].Length >= 79 && errorIndex + 1 < lines.Length)
            error += lines[errorIndex + 1].Trim();

        var lineNumber = FindLineNumber(lines, errorIndex);
        var entry = lineNumber is { } n ? FindEntry(texPath, n) : null;

        var sb = new StringBuilder("LuaLaTeX konnte das Buch nicht setzen.\n\n");
        sb.AppendLine(error is null ? "Die Ursache ließ sich nicht automatisch ermitteln." : $"Fehler: {error}");
        if (entry is not null)
            sb.AppendLine($"Betroffener Eintrag: {entry} (Zeile {lineNumber} in {Path.GetFileName(texPath)})");
        else if (lineNumber is not null)
            sb.AppendLine($"Stelle: Zeile {lineNumber} in {Path.GetFileName(texPath)}");

        var hint = Hint(output);
        if (hint is not null)
            sb.AppendLine().AppendLine(hint);

        sb.AppendLine().Append($"Vollständiges Protokoll: {logPath}");
        return sb.ToString();
    }

    private static int? FindLineNumber(string[] lines, int errorIndex)
    {
        var from = Math.Max(0, errorIndex);
        foreach (var line in lines.Skip(from).Take(30))
        {
            var match = Regex.Match(line, @"^l\.(\d+)|after line (\d+)|on input line (\d+)");
            if (match.Success)
                return int.Parse(match.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value);
        }
        return null;
    }

    /// <summary>Sucht oberhalb der Fehlerzeile den Eintragskopf und liefert „Datum – Titel“.</summary>
    private static string? FindEntry(string texPath, int lineNumber)
    {
        if (!File.Exists(texPath)) return null;
        var tex = File.ReadAllLines(texPath);
        for (var i = Math.Min(lineNumber, tex.Length) - 1; i >= 0; i--)
        {
            var head = Regex.Match(tex[i], @"^\\DiaryEntryHead\{(?<date>[^}]*)\}\{(?<title>[^}]*)\}");
            if (head.Success)
            {
                var date = head.Groups["date"].Value;
                var title = head.Groups["title"].Value;
                return string.IsNullOrWhiteSpace(title) || date.Contains(title) ? date : $"{date} – {title}";
            }
            if (tex[i].StartsWith("\\DiaryMonth")) return null;
        }
        return null;
    }

    private static string? Hint(string output)
    {
        if (output.Contains("I can't write on file", StringComparison.OrdinalIgnoreCase))
            return "Hinweis: Die PDF ist vermutlich noch in einem PDF-Viewer geöffnet. Viewer schließen und erneut versuchen.";
        var missingFile = Regex.Match(output, @"File `(?<file>[^']+)' not found");
        if (missingFile.Success)
            return $"Hinweis: Die Datei „{missingFile.Groups["file"].Value}“ fehlt. Bei einem Bild bitte den Export erneut erzeugen; " +
                   "bei einer .sty-Datei fehlt ein LaTeX-Paket (in der MiKTeX Console „Updates“ und „Pakete“ prüfen).";
        return null;
    }
}
