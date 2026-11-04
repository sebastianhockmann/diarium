using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiariumBookStudio.Services;

/// <param name="File">Kennung des Fotos; die tatsächliche Datei liefert der Rückruf in Render.</param>
/// <param name="Aspect">Breite / Höhe.</param>
/// <param name="Caption">Bereits für LaTeX maskierte Bildunterschrift.</param>
/// <param name="PixelWidth">Breite in Pixeln (nach EXIF-Drehung), für die Auflösungsprüfung.</param>
/// <param name="SourceName">Originaldateiname, für Hinweise.</param>
public sealed record GalleryPhoto(string File, double Aspect, string Caption, double PixelWidth, string SourceName);

/// <summary>
/// Berechnet ein Fotolayout ohne Beschnitt: Bilder werden in Zeilen gruppiert,
/// jede Zeile wird so skaliert, dass sie exakt die Textbreite füllt („Blocksatz-Reihen“).
/// Drei Bilder werden – wenn es passt – als großes Bild mit zwei kleinen daneben gesetzt.
/// Alle Maße sind Anteile von \linewidth.
/// </summary>
public static class GalleryLayout
{
    private const double Gap = 0.02;               // ca. 2,3 mm bei A5
    private const double TargetRowHeight = 0.36;   // ca. 41 mm
    private const double SingleMaxHeight = 0.75;   // einzelnes Bild höchstens ca. 86 mm hoch
    private const double LastRowMaxHeight = 0.48;  // letzte Zeile soll nicht dominieren
    private const int MaxPerRow = 4;
    private const double Safety = 0.0005;          // Rundungsreserve gegen „Overfull hbox“

    /// <param name="place">Wird für jedes Foto mit seiner Breite (Anteil von \linewidth) aufgerufen und
    /// liefert den Pfad der passend zur Druckgröße geschriebenen Datei.</param>
    public static string Render(IReadOnlyList<GalleryPhoto> photos, Func<GalleryPhoto, double, string> place)
    {
        if (photos.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("\\begin{DiaryGallery}");
        if (!(photos.Count == 3 && TryRenderFeature(photos, sb, place)))
        {
            var rows = PartitionRows(photos);
            for (var r = 0; r < rows.Count; r++)
            {
                if (r > 0) sb.AppendLine($"\\par\\vspace{{{F(Gap)}\\linewidth}}");
                var maxHeight = rows.Count == 1 ? SingleMaxHeight : LastRowMaxHeight;
                RenderRow(rows[r], r == rows.Count - 1 ? maxHeight : double.MaxValue, sb, place);
            }
        }
        sb.AppendLine("\\end{DiaryGallery}");
        return sb.ToString();
    }

    private static double RowHeight(IReadOnlyList<GalleryPhoto> row)
        => (1.0 - Gap * (row.Count - 1)) / row.Sum(p => p.Aspect);

    private static void RenderRow(IReadOnlyList<GalleryPhoto> row, double maxHeight, StringBuilder sb, Func<GalleryPhoto, double, string> place)
    {
        var height = Math.Min(RowHeight(row), maxHeight);
        var cells = row.Select(p => $"\\DiaryPhoto{{{F(p.Aspect * height - Safety)}}}{{{place(p, p.Aspect * height)}}}{{{p.Caption}}}").ToList();
        sb.AppendLine("\\DiaryGalleryRow");
        sb.AppendLine("\\makebox[\\linewidth][c]{" + string.Join($"\\hspace{{{F(Gap)}\\linewidth}}", cells) + "}");
    }

    /// <summary>Großes Bild links, zwei Bilder rechts übereinander – beide Seiten gleich hoch.</summary>
    private static bool TryRenderFeature(IReadOnlyList<GalleryPhoto> photos, StringBuilder sb, Func<GalleryPhoto, double, string> place)
    {
        if (photos.Any(p => !string.IsNullOrEmpty(p.Caption))) return false;

        var a0 = photos[0].Aspect;
        var s = 1.0 / photos[1].Aspect + 1.0 / photos[2].Aspect;
        // Links: Höhe w0/a0. Rechts: w1·s + Gap. Gleichsetzen mit w0 + w1 + Gap = 1:
        var w1 = (1.0 - Gap - a0 * Gap) / (a0 * s + 1.0);
        var w0 = 1.0 - Gap - w1;
        var height = w0 / a0;
        if (w1 < 0.28 || w1 > 0.5 || height > SingleMaxHeight) return false;
        var file0 = place(photos[0], w0);
        var file1 = place(photos[1], w1);
        var file2 = place(photos[2], w1);

        var innerGap = Gap / w1; // innerhalb der rechten Spalte ist \linewidth = w1
        sb.AppendLine("\\DiaryGalleryRow");
        sb.Append($"\\DiaryPhoto{{{F(w0 - Safety)}}}{{{file0}}}{{}}");
        sb.Append($"\\hspace{{{F(Gap)}\\linewidth}}");
        sb.AppendLine($"\\begin{{minipage}}[t]{{{F(w1 - Safety)}\\linewidth}}\\vspace{{0pt}}%");
        sb.AppendLine($"\\DiaryPhoto{{1}}{{{file1}}}{{}}\\par\\vspace{{{F(innerGap)}\\linewidth}}\\nointerlineskip");
        sb.AppendLine($"\\noindent\\DiaryPhoto{{1}}{{{file2}}}{{}}%");
        sb.AppendLine("\\end{minipage}");
        return true;
    }

    /// <summary>
    /// Teilt die Bilder (Reihenfolge bleibt erhalten) so in Zeilen auf, dass die Zeilenhöhen
    /// möglichst nah an der Zielhöhe liegen (dynamische Programmierung über alle Aufteilungen).
    /// </summary>
    private static List<List<GalleryPhoto>> PartitionRows(IReadOnlyList<GalleryPhoto> photos)
    {
        var n = photos.Count;
        var best = new double[n + 1];
        var split = new int[n + 1];
        for (var i = 1; i <= n; i++) best[i] = double.MaxValue;

        for (var end = 1; end <= n; end++)
        {
            for (var start = Math.Max(0, end - MaxPerRow); start < end; start++)
            {
                if (best[start] == double.MaxValue) continue;
                var row = photos.Skip(start).Take(end - start).ToList();
                var height = RowHeight(row);
                if (end == n) height = Math.Min(height, n == row.Count ? SingleMaxHeight : LastRowMaxHeight);
                var cost = best[start] + Math.Pow(height - TargetRowHeight, 2);
                if (cost < best[end])
                {
                    best[end] = cost;
                    split[end] = start;
                }
            }
        }

        var rows = new List<List<GalleryPhoto>>();
        for (var end = n; end > 0; end = split[end])
            rows.Insert(0, photos.Skip(split[end]).Take(end - split[end]).ToList());
        return rows;
    }

    private static string F(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
