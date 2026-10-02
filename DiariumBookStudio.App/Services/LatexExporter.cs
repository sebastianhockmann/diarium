using System.Text;
using DiariumBookStudio.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace DiariumBookStudio.Services;

public static class LatexExporter
{
    public static string Export(BookProject project, string? outputTexPath = null)
    {
        var texPath = string.IsNullOrWhiteSpace(outputTexPath)
            ? Path.Combine(project.ProjectRoot, "output", "book.tex")
            : outputTexPath;

        var outputDir = Path.GetDirectoryName(texPath) ?? Path.Combine(project.ProjectRoot, "output");
        var imageDir = Path.Combine(outputDir, "images");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(imageDir);

        var mapDebugPath = Path.Combine(outputDir, "map_debug.txt");
        File.WriteAllText(mapDebugPath, $"DiariumBookStudio Karten-Debug {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nExportordner: {outputDir}\n\n", Encoding.UTF8);

        var tex = new StringBuilder();
        tex.AppendLine("% Automatisch erzeugt mit DiariumBookStudio");
        tex.AppendLine("% Mit LuaLaTeX kompilieren: lualatex book.tex");
        tex.AppendLine("\\documentclass[11pt,a5paper,openany]{scrbook}");
        tex.AppendLine("\\usepackage[ngerman]{babel}");
        tex.AppendLine("\\usepackage{fontspec}");
        tex.AppendLine("\\IfFontExistsTF{Libertinus Serif}{\\setmainfont{Libertinus Serif}}{\\setmainfont{Latin Modern Roman}}");
        tex.AppendLine("\\usepackage{graphicx}");
        tex.AppendLine("\\usepackage{geometry}");
        tex.AppendLine("\\usepackage{xcolor}");
        tex.AppendLine("\\usepackage{microtype}");
        tex.AppendLine("\\usepackage{needspace}");
        tex.AppendLine("\\usepackage{hyperref}");
        tex.AppendLine("\\usepackage{bookmark}");
        tex.AppendLine("\\usepackage{lastpage}");
        tex.AppendLine("\\usepackage[automark]{scrlayer-scrpage}");
        tex.AppendLine("\\geometry{inner=16mm,outer=14mm,top=16mm,bottom=20mm,footskip=12mm}");
        tex.AppendLine("\\clearpairofpagestyles");
        tex.AppendLine("\\cfoot*{\\small\\textcolor{MetaGray}{Seite \\thepage{} von \\pageref*{LastPage}}}");
        tex.AppendLine("\\renewcommand*{\\chapterpagestyle}{scrheadings}");
        tex.AppendLine("\\pagestyle{scrheadings}");
        tex.AppendLine("\\setlength{\\parindent}{0pt}");
        tex.AppendLine("\\setlength{\\parskip}{0.65em}");
        tex.AppendLine("\\definecolor{MetaGray}{HTML}{666666}");
        tex.AppendLine("\\definecolor{CardBg}{HTML}{F7F7F7}");
        tex.AppendLine("\\newcommand{\\entrymeta}[1]{{\\small\\textcolor{MetaGray}{#1}}}");
        tex.AppendLine("\\begin{document}");
        tex.AppendLine("\\frontmatter");
        tex.AppendLine($"\\title{{{Esc(project.BookTitle)}}}");
        tex.AppendLine("\\author{}");
        tex.AppendLine("\\date{}");
        tex.AppendLine("\\maketitle");
        tex.AppendLine("\\tableofcontents");
        tex.AppendLine("\\mainmatter");

        string? currentMonth = null;
        foreach (var entry in project.Entries.OrderBy(e => e.Date ?? DateTime.MaxValue))
        {
            var month = entry.Date?.ToString("MMMM yyyy", new CultureInfo("de-DE")) ?? "Ohne Datum";
            if (currentMonth != month)
            {
                currentMonth = month;
                tex.AppendLine($"\\chapter{{{Esc(month)}}}");
            }

            var dateTitle = entry.Date?.ToString("dddd, d. MMMM yyyy", new CultureInfo("de-DE")) ?? entry.DateText;
            var titleOnly = string.IsNullOrWhiteSpace(entry.Title) ? dateTitle : entry.Title;
            var tocTitle = string.IsNullOrWhiteSpace(entry.Title) ? dateTitle : $"{dateTitle} – {entry.Title}";

            tex.AppendLine("\\Needspace{12\\baselineskip}");
            tex.AppendLine("\\noindent");
            tex.AppendLine("\\begin{minipage}[t]{0.56\\textwidth}");
            tex.AppendLine("\\vspace{0pt}");
            tex.AppendLine($"{{\\small\\textcolor{{MetaGray}}{{📅 {Esc(dateTitle)}}}}}\\par");
            tex.AppendLine("\\vspace{0.35em}");
            tex.AppendLine($"{{\\Large\\bfseries {Esc(titleOnly)}}}\\par");
            tex.AppendLine("\\vspace{0.55em}");

            foreach (var line in BuildWeatherLines(entry))
                tex.AppendLine($"{{\\small\\textcolor{{MetaGray}}{{🌤️ {Esc(line)}}}}}\\par");

            tex.AppendLine("\\end{minipage}");
            var mapFileName = TryCreateMapImage(entry, imageDir, mapDebugPath);
            if (!string.IsNullOrWhiteSpace(mapFileName))
            {
                tex.AppendLine("\\hspace{0.06\\textwidth}");
                tex.AppendLine("\\begin{minipage}[t]{0.36\\textwidth}");
                tex.AppendLine("\\vspace{0pt}");
                tex.AppendLine($"\\includegraphics[width=\\linewidth]{{images/{mapFileName}}}");
                tex.AppendLine("\\end{minipage}");
            }

            tex.AppendLine($"\\addcontentsline{{toc}}{{section}}{{{Esc(tocTitle)}}}");
            tex.AppendLine("\\par\\vspace{1.1em}\\noindent");

            tex.AppendLine("\\noindent " + Esc(entry.EditedText).Replace("\n\n", "\n\n\\par\\noindent\n"));
            tex.AppendLine("\\vspace{0.8em}");

            var selected = entry.Images.Where(i => i.Selected).OrderBy(i => i.Order).ToList();
            var imgIndex = 1;
            var gallery = selected
                .Select(img => new { Img = img, File = CopyImageForEntry(img, entry, imageDir, ref imgIndex) })
                .Where(x => !string.IsNullOrWhiteSpace(x.File))
                .ToList();

            if (gallery.Any())
            {
                tex.AppendLine("\\vspace{0.3em}");
                for (var i = 0; i < gallery.Count; i += 3)
                {
                    var row = gallery.Skip(i).Take(3).ToList();
                    tex.AppendLine("\\noindent");
                    for (var c = 0; c < row.Count; c++)
                    {
                        var item = row[c];
                        tex.AppendLine("\\begin{minipage}[t]{0.32\\linewidth}");
                        tex.AppendLine($"\\includegraphics[width=\\linewidth,height=34mm,keepaspectratio]{{images/{item.File}}}");
                        if (!string.IsNullOrWhiteSpace(item.Img.Caption))
                            tex.AppendLine($"\\par{{\\scriptsize\\textcolor{{MetaGray}}{{{Esc(item.Img.Caption)}}}}}");
                        tex.AppendLine("\\end{minipage}");
                        if (c < row.Count - 1)
                            tex.AppendLine("\\hfill");
                    }
                    tex.AppendLine("\\par\\vspace{0.35em}");
                }
            }
        }

        tex.AppendLine("\\end{document}");
        File.WriteAllText(texPath, tex.ToString(), Encoding.UTF8);
        return texPath;
    }

    private static string? CopyImageForEntry(ImageItem img, EntryModel entry, string imageDir, ref int imgIndex)
    {
        if (!File.Exists(img.FullPath)) return null;
        var ext = Path.GetExtension(img.FullPath);
        var datePrefix = entry.Date?.ToString("yyyy-MM-dd") ?? "ohne-datum";
        var safeName = $"{datePrefix}_{imgIndex:00}{ext}";
        var target = Path.Combine(imageDir, safeName);
        File.Copy(img.FullPath, target, true);
        imgIndex++;
        return safeName;
    }

    private static string? TryCreateMapImage(EntryModel entry, string imageDir, string mapDebugPath)
    {
        if (!TryParseLatLon(entry.Location, out var lat, out var lon))
        {
            try
            {
                File.AppendAllText(mapDebugPath, $"KEINE KOORDINATEN ERKANNT: {entry.DateText} / Location='{entry.Location}'\n");
            }
            catch { }
            return null;
        }

        var datePrefix = entry.Date?.ToString("yyyy-MM-dd") ?? "ohne-datum";
        var fileName = $"{datePrefix}_map.png";
        var filePath = Path.Combine(imageDir, fileName);
        var logPath = mapDebugPath;

        try
        {
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Karte: {entry.DateText} / {lat.ToString(CultureInfo.InvariantCulture)}, {lon.ToString(CultureInfo.InvariantCulture)}\n");
        }
        catch { }

        if (File.Exists(filePath))
            return fileName;

        try
        {
            CreateOpenStreetMapTileImage(lat, lon, filePath, logPath);
            return File.Exists(filePath) ? fileName : null;
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(logPath, $"FEHLER: {ex.GetType().Name}: {ex.Message}\n");
                CreateMapErrorImage(lat, lon, filePath, ex.Message);
                return File.Exists(filePath) ? fileName : null;
            }
            catch
            {
                return null;
            }
        }
    }

    private static void CreateOpenStreetMapTileImage(double lat, double lon, string targetFilePath, string logPath)
    {
        const int zoom = 15;
        const int tileSize = 256;
        const int outputWidth = 420;
        const int outputHeight = 240;

        var center = LatLonToPixel(lat, lon, zoom);
        var startX = center.X - outputWidth / 2.0;
        var startY = center.Y - outputHeight / 2.0;

        var firstTileX = (int)Math.Floor(startX / tileSize);
        var firstTileY = (int)Math.Floor(startY / tileSize);
        var lastTileX = (int)Math.Floor((startX + outputWidth) / tileSize);
        var lastTileY = (int)Math.Floor((startY + outputHeight) / tileSize);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiariumBookStudio/1.0");
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.openstreetmap.org/");

        var visual = new System.Windows.Media.DrawingVisual();
        var downloadedTiles = 0;

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239, 239, 239)), null, new Rect(0, 0, outputWidth, outputHeight));

            for (var x = firstTileX; x <= lastTileX; x++)
            {
                for (var y = firstTileY; y <= lastTileY; y++)
                {
                    var urls = new[]
                    {
                        $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png",
                        $"https://a.tile.openstreetmap.org/{zoom}/{x}/{y}.png",
                        $"https://b.tile.openstreetmap.org/{zoom}/{x}/{y}.png",
                        $"https://c.tile.openstreetmap.org/{zoom}/{x}/{y}.png"
                    };

                    byte[]? bytes = null;
                    Exception? lastError = null;

                    foreach (var url in urls)
                    {
                        try
                        {
                            bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                            if (bytes.Length > 1000)
                            {
                                try { File.AppendAllText(logPath, $"OK Tile {url} ({bytes.Length} bytes)\n"); } catch { }
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            bytes = null;
                        }
                    }

                    if (bytes == null)
                    {
                        try { File.AppendAllText(logPath, $"Tile fehlgeschlagen z={zoom} x={x} y={y}: {lastError?.Message}\n"); } catch { }
                        continue;
                    }

                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        var drawX = x * tileSize - startX;
                        var drawY = y * tileSize - startY;
                        dc.DrawImage(bitmap, new Rect(drawX, drawY, tileSize, tileSize));
                        downloadedTiles++;
                    }
                    catch (Exception ex)
                    {
                        try { File.AppendAllText(logPath, $"Bitmap-Fehler: {ex.Message}\n"); } catch { }
                    }
                }
            }

            if (downloadedTiles == 0)
            {
                throw new InvalidOperationException("Keine OpenStreetMap-Kachel konnte geladen werden.");
            }

            DrawMapMarker(dc, outputWidth / 2.0, outputHeight / 2.0);

            var osm = new FormattedText(
                "© OpenStreetMap",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                10,
                Brushes.Black,
                1.0);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(205, 255, 255, 255)), null, new Rect(outputWidth - 104, outputHeight - 18, 104, 18));
            dc.DrawText(osm, new Point(outputWidth - 100, outputHeight - 15));
        }

        SaveVisualAsPng(visual, outputWidth, outputHeight, targetFilePath);
    }

    private static void CreateMapErrorImage(double lat, double lon, string targetFilePath, string errorText)
    {
        const int outputWidth = 420;
        const int outputHeight = 240;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 245, 245)), new Pen(new SolidColorBrush(Color.FromRgb(210, 210, 210)), 1), new Rect(0, 0, outputWidth, outputHeight));

            var title = new FormattedText(
                "Karte konnte nicht geladen werden",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                15,
                Brushes.Black,
                1.0);
            dc.DrawText(title, new Point(18, 45));

            var coords = new FormattedText(
                $"{lat.ToString("0.000000", CultureInfo.InvariantCulture)}, {lon.ToString("0.000000", CultureInfo.InvariantCulture)}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                13,
                new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                1.0);
            dc.DrawText(coords, new Point(18, 82));

            var hint = new FormattedText(
                "Details: map_debug.txt im Exportordner",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                11,
                new SolidColorBrush(Color.FromRgb(110, 110, 110)),
                1.0);
            dc.DrawText(hint, new Point(18, 112));

            DrawMapMarker(dc, outputWidth - 48, 48);
        }

        SaveVisualAsPng(visual, outputWidth, outputHeight, targetFilePath);
    }

    private static void DrawMapMarker(System.Windows.Media.DrawingContext dc, double x, double y)
    {
        dc.DrawEllipse(Brushes.White, null, new Point(x, y), 9, 9);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(42, 129, 194)), new Pen(Brushes.White, 2), new Point(x, y), 7, 7);
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(18, 93, 150)), null, new Point(x, y), 3, 3);
    }

    private static void SaveVisualAsPng(System.Windows.Media.DrawingVisual visual, int width, int height, string targetFilePath)
    {
        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
        using var fileStream = File.Create(targetFilePath);
        encoder.Save(fileStream);
    }

    private static (double X, double Y) LatLonToPixel(double lat, double lon, int zoom)
    {
        var sinLat = Math.Sin(lat * Math.PI / 180.0);
        var mapSize = 256.0 * Math.Pow(2, zoom);
        var x = (lon + 180.0) / 360.0 * mapSize;
        var y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * mapSize;
        return (x, y);
    }

    private static bool TryParseLatLon(string? value, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Diarium schreibt den Ort z.B. als "📍 50.882034, 12.065572".
        // Deshalb nicht stumpf splitten, sondern die ersten zwei Koordinaten aus dem Text ziehen.
        var match = System.Text.RegularExpressions.Regex.Match(
            value,
            @"(?<lat>[+-]?\d{1,2}[\.,]\d+)\s*,\s*(?<lon>[+-]?\d{1,3}[\.,]\d+)"
        );

        if (!match.Success) return false;

        var latText = match.Groups["lat"].Value.Replace(',', '.');
        var lonText = match.Groups["lon"].Value.Replace(',', '.');

        return double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out lon);
    }

    private static string[] BuildWeatherLines(EntryModel entry)
    {
        var lines = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Weather))
        {
            var weatherText = entry.Weather.Trim();
            weatherText = System.Text.RegularExpressions.Regex.Replace(
                weatherText,
                @"^(Wetter\s*:\s*)+",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            ).Trim();

            if (!string.IsNullOrWhiteSpace(weatherText))
                lines.Add($"Wetter: {weatherText}");
        }
        return lines.ToArray();
    }

    private static string Esc(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("\\", "\\textbackslash{}")
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("$", "\\$")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("~", "\\textasciitilde{}")
            .Replace("^", "\\textasciicircum{}");
    }
}
