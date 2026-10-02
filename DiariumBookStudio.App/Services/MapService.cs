using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiariumBookStudio.Services;

/// <summary>
/// Kartenausschnitte aus OpenStreetMap-Kacheln und Ortsnamen über Nominatim.
/// Hält sich an die OSM-Nutzungsrichtlinien: eindeutiger User-Agent, Kachel-Cache
/// im Projektordner, höchstens eine Nominatim-Anfrage pro Sekunde.
/// Muss auf einem STA-Thread laufen (WPF-Rendering).
/// </summary>
public sealed class MapService
{
    // Logische Größe des Kartenausschnitts; gerendert wird mit doppelter Auflösung für den Druck.
    private const int MapWidth = 420;
    private const int MapHeight = 240;
    private const int Zoom = 15;
    private const int Scale = 2;
    private const int TileSize = 256;

    private static readonly HttpClient Http = CreateClient();
    private static DateTime _lastNominatimRequest = DateTime.MinValue;

    private readonly string _tileCacheDir;
    private readonly Action<string> _log;
    private readonly Dictionary<string, string?> _placeCache = new();
    private bool _geocodingDisabled;

    public MapService(string tileCacheDir, Action<string> log)
    {
        _tileCacheDir = tileCacheDir;
        _log = log;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiariumBookStudio/21 (+https://github.com/sebastianhockmann/diarium)");
        return client;
    }

    public static bool TryParseLatLon(string? value, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Diarium schreibt den Ort z. B. als „📍 50.882034, 12.065572“.
        var match = Regex.Match(value, @"(?<lat>[+-]?\d{1,2}\.\d+)\s*,\s*(?<lon>[+-]?\d{1,3}\.\d+)");
        return match.Success
            && double.TryParse(match.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(match.Groups["lon"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lon);
    }

    /// <summary>Erzeugt (oder verwendet) eine Karten-PNG. Gibt bei Fehlern null zurück – dann erscheint keine Karte.</summary>
    public string? CreateMap(double lat, double lon, string imageDir)
    {
        var fileName = string.Create(CultureInfo.InvariantCulture, $"map_{lat:0.00000}_{lon:0.00000}.png");
        var filePath = Path.Combine(imageDir, fileName);
        if (File.Exists(filePath)) return fileName;

        try
        {
            RenderMap(lat, lon, filePath);
            return fileName;
        }
        catch (Exception ex)
        {
            _log($"Karte fehlgeschlagen ({lat.ToString(CultureInfo.InvariantCulture)}, {lon.ToString(CultureInfo.InvariantCulture)}): {ex.Message}");
            return null;
        }
    }

    private void RenderMap(double lat, double lon, string targetFilePath)
    {
        // Gleicher Ausschnitt wie Zoom 15 bei 420×240, aber mit Zoom 16 und 840×480 Pixeln.
        var zoom = Zoom + (Scale == 2 ? 1 : 0);
        var width = MapWidth * Scale;
        var height = MapHeight * Scale;

        var center = LatLonToPixel(lat, lon, zoom);
        var startX = center.X - width / 2.0;
        var startY = center.Y - height / 2.0;

        var visual = new DrawingVisual();
        var loaded = 0;
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239, 239, 239)), null, new Rect(0, 0, width, height));

            for (var x = (int)Math.Floor(startX / TileSize); x <= (int)Math.Floor((startX + width) / TileSize); x++)
            {
                for (var y = (int)Math.Floor(startY / TileSize); y <= (int)Math.Floor((startY + height) / TileSize); y++)
                {
                    var tile = LoadTile(zoom, x, y);
                    if (tile is null) continue;
                    dc.DrawImage(tile, new Rect(x * TileSize - startX, y * TileSize - startY, TileSize, TileSize));
                    loaded++;
                }
            }

            if (loaded == 0)
                throw new InvalidOperationException("Keine OpenStreetMap-Kachel konnte geladen werden.");

            DrawPin(dc, width / 2.0, height / 2.0, Scale);

            var attribution = new FormattedText("© OpenStreetMap-Mitwirkende", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9 * Scale, Brushes.DimGray, 1.0);
            var box = new Rect(width - attribution.Width - 8 * Scale, height - attribution.Height - 2 * Scale,
                attribution.Width + 8 * Scale, attribution.Height + 2 * Scale);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null, box);
            dc.DrawText(attribution, new Point(box.X + 4 * Scale, box.Y + Scale));
        }

        // 96 dpi: dann entspricht eine Zeicheneinheit genau einem Pixel. Die Druckgröße legt LaTeX fest.
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(targetFilePath);
        encoder.Save(stream);
    }

    private BitmapImage? LoadTile(int zoom, int x, int y)
    {
        var max = 1 << zoom;
        x = ((x % max) + max) % max;
        if (y < 0 || y >= max) return null;

        var cachePath = Path.Combine(_tileCacheDir, zoom.ToString(), x.ToString(), $"{y}.png");
        try
        {
            if (!File.Exists(cachePath))
            {
                var bytes = Http.GetByteArrayAsync($"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png").GetAwaiter().GetResult();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                File.WriteAllBytes(cachePath, bytes);
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(cachePath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            _log($"Kachel z={zoom} x={x} y={y} fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private static void DrawPin(DrawingContext dc, double x, double y, double scale)
    {
        var accent = new SolidColorBrush(Color.FromRgb(0x2E, 0x6A, 0x70));
        var r = 7 * scale;
        // Tropfenform: Kreis oben, Spitze auf dem Ort
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y), true, true);
            ctx.LineTo(new Point(x - r * 0.85, y - r * 1.6), true, true);
            ctx.ArcTo(new Point(x + r * 0.85, y - r * 1.6), new Size(r, r), 0, true, SweepDirection.Clockwise, true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(accent, new Pen(Brushes.White, 1.5 * scale), geometry);
        dc.DrawEllipse(Brushes.White, null, new Point(x, y - r * 2.05), r * 0.38, r * 0.38);
    }

    private static (double X, double Y) LatLonToPixel(double lat, double lon, int zoom)
    {
        var sinLat = Math.Sin(lat * Math.PI / 180.0);
        var mapSize = TileSize * Math.Pow(2, zoom);
        var x = (lon + 180.0) / 360.0 * mapSize;
        var y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * mapSize;
        return (x, y);
    }

    /// <summary>Ermittelt einen Ortsnamen wie „Gera“ (Ausland: „Prag, Tschechien“). Null bei Fehlern.</summary>
    public string? ReverseGeocode(double lat, double lon)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{lat:0.000},{lon:0.000}");
        if (_placeCache.TryGetValue(key, out var cached)) return cached;

        if (_geocodingDisabled) return null;

        string? place = null;
        try
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=14&accept-language=de&lat={lat}&lon={lon}");
            var json = GetNominatim(url);
            if (json is null)
            {
                // Nominatim drosselt: für diesen Export aufhören, nächster Export versucht es erneut.
                _geocodingDisabled = true;
                _log("Nominatim lehnt Anfragen ab (429). Ortsnamen werden beim nächsten Export erneut versucht.");
                return null;
            }
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("address", out var address))
            {
                string? Get(string name) => address.TryGetProperty(name, out var v) ? v.GetString() : null;
                var locality = new[] { "village", "town", "city", "municipality", "hamlet", "suburb" }
                    .Select(Get).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                var country = Get("country");
                var countryCode = Get("country_code");
                if (!string.IsNullOrWhiteSpace(locality))
                    place = countryCode is null or "de" ? locality : $"{locality}, {country}";
            }
        }
        catch (Exception ex)
        {
            _log($"Ortsname für {key} nicht ermittelt: {ex.Message}");
        }

        _placeCache[key] = place;
        return place;
    }

    /// <summary>Höchstens eine Anfrage pro Sekunde; bei 429 einmal mit Pause wiederholen. Null, wenn weiter gedrosselt.</summary>
    private static string? GetNominatim(string url)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var wait = TimeSpan.FromSeconds(attempt == 0 ? 1.1 : 5) - (DateTime.UtcNow - _lastNominatimRequest);
            if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            _lastNominatimRequest = DateTime.UtcNow;

            using var response = Http.GetAsync(url).GetAwaiter().GetResult();
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) continue;
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        return null;
    }
}
