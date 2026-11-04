using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiariumBookStudio.Services;

/// <summary>Startet LuaLaTeX und pdftoppm und wertet deren Ausgabe aus.</summary>
public static class LatexRunner
{
    /// <summary>Ein LuaLaTeX-Lauf. Liefert die Ausgabe; bei Fehlern eine verständliche Meldung (siehe LatexErrorReport).</summary>
    public static async Task<string> RunAsync(string command, string texPath, string jobName)
    {
        var workingDirectory = Path.GetDirectoryName(texPath)
            ?? throw new InvalidOperationException("Der Ausgabeordner konnte nicht bestimmt werden.");

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = $"-interaction=nonstopmode -halt-on-error -jobname=\"{jobName}\" \"{Path.GetFileName(texPath)}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        Process? started;
        try
        {
            started = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            started = null;
        }
        using var process = started
            ?? throw new InvalidOperationException("LuaLaTeX wurde nicht gefunden. Ist MiKTeX installiert und „lualatex“ im PATH? " +
                                                   "Nach der Installation von MiKTeX die App neu starten oder unter Datei → Einstellungen den Pfad angeben.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var logPath = Path.Combine(workingDirectory, "lualatex_error.txt");
            await File.WriteAllTextAsync(logPath, stdout + Environment.NewLine + stderr, Encoding.UTF8);
            throw new InvalidOperationException(LatexErrorReport.Describe(stdout, texPath, logPath));
        }
        return stdout;
    }

    /// <summary>Seitenzahl aus der LuaLaTeX-Ausgabe („Output written on … (281 pages, …)“).</summary>
    public static int PageCount(string output)
    {
        // TeX bricht lange Protokollzeilen um – daher auch über Zeilenenden hinweg suchen.
        var match = Regex.Match(output, @"Output\s+written\s+on\s.*?\(\s*(\d+)\s+pages?", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidOperationException("Die Seitenzahl des PDFs konnte nicht aus der LuaLaTeX-Ausgabe ermittelt werden.");
        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>
    /// Seitenmarken aus dem LuaLaTeX-Protokoll: für jeden Eintrag die Seite, auf der er beginnt
    /// (siehe \DiaryEntryKey in diarybook.sty).
    /// </summary>
    public static IReadOnlyList<(int Page, string Key)> EntryPages(string logPath)
    {
        if (!File.Exists(logPath)) return Array.Empty<(int, string)>();
        return Regex.Matches(File.ReadAllText(logPath), @"^DIARYPAGE:(\d+):(\S+)\s*$", RegexOptions.Multiline)
            .Select(m => (int.Parse(m.Groups[1].Value), m.Groups[2].Value))
            .ToList();
    }

    /// <summary>Rendert alle Seiten eines PDFs als PNG (pdftoppm, liegt bei MiKTeX und TeX Live bei).</summary>
    /// <returns>Die PNG-Dateien in Seitenreihenfolge.</returns>
    public static async Task<IReadOnlyList<string>> RenderPagesAsync(string command, string pdfPath, string targetDir, int dpi)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var old in Directory.GetFiles(targetDir, "p-*.png"))
            File.Delete(old);

        var psi = new ProcessStartInfo(command, $"-r {dpi} -png \"{pdfPath}\" \"{Path.Combine(targetDir, "p")}\"")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
        };
        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            process = null;
        }
        if (process is null)
            throw new InvalidOperationException("pdftoppm wurde nicht gefunden (gehört zu MiKTeX). Die Vorschau-PDF liegt unter:\n" + pdfPath);
        using (process)
        {
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Die Vorschau konnte nicht gerendert werden: {error.Trim()}");
        }

        // pdftoppm nummeriert mit führenden Nullen passend zur Seitenzahl (p-1.png bzw. p-01.png …).
        return Directory.GetFiles(targetDir, "p-*.png")
            .OrderBy(f => int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(f), @"\d+$").Value))
            .ToList();
    }
}
