using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

/// <param name="Fingerprint">Bild-Fingerabdruck (dHash, 64 Bit), ähnliche Bilder unterscheiden sich in wenigen Bits.</param>
/// <param name="FileSize">Dateigröße als einfaches Maß für Details/Schärfe (bei gleicher Pixelzahl).</param>
public sealed record PhotoInfo(ulong Fingerprint, long FileSize);

/// <summary>
/// Automatische Fotoauswahl: fast gleiche Fotos (Serienbilder, Duplikate) werden zu einem Foto
/// zusammengefasst – es bleibt das detailreichste. Sind dann noch mehr Fotos übrig als erlaubt,
/// wird gleichmäßig über den Eintrag verteilt ausgewählt, damit der ganze Tag abgebildet ist.
/// Diarium exportiert die Fotos nicht in Aufnahmereihenfolge; sie wird über die fortlaufende
/// Bildnummer der Kamera (IMG_1234) wiederhergestellt, siehe <see cref="CaptureOrder"/>.
/// </summary>
public static class PhotoSelection
{
    /// <summary>
    /// Höchstens so viele abweichende Bits gelten als „fast gleich“ (von 64). Am echten Export geprüft:
    /// bis 10 nur echte Duplikate/Serien, ab etwa 14 werden schon verschiedene Motive zusammengelegt.
    /// </summary>
    public const int SimilarBits = 10;

    /// <summary>
    /// Wählt bis zu <paramref name="max"/> Fotos aus und gibt ihre Indizes (aufsteigend) zurück.
    /// Die Fotos müssen in Aufnahmereihenfolge übergeben werden.
    /// </summary>
    /// <param name="required">Indizes, die immer dazugehören (z. B. das Titelbild).</param>
    public static IReadOnlyList<int> Choose(IReadOnlyList<PhotoInfo> photos, int max, IReadOnlyCollection<int>? required = null)
    {
        if (photos.Count == 0 || max <= 0) return Array.Empty<int>();
        required ??= Array.Empty<int>();

        // Gruppen bilden: ein Foto gehört zu einer Gruppe, wenn es deren erstem Foto ähnelt –
        // über den ganzen Eintrag, nicht nur direkt hintereinander (dasselbe Motiv später noch einmal).
        var series = new List<List<int>>();
        foreach (var index in Enumerable.Range(0, photos.Count))
        {
            var group = series.Find(s => Distance(photos[s[0]].Fingerprint, photos[index].Fingerprint) <= SimilarBits);
            if (group is not null)
                group.Add(index);
            else
                series.Add(new List<int> { index });
        }

        // Pro Serie ein Foto: ein Pflichtfoto, sonst das detailreichste.
        var representatives = series
            .Select(s => s.FirstOrDefault(required.Contains, s.OrderByDescending(i => photos[i].FileSize).First()))
            .ToList();

        var chosen = new SortedSet<int>(required.Where(i => i >= 0 && i < photos.Count));
        var candidates = representatives.Where(i => !chosen.Contains(i)).ToList();
        var free = max - chosen.Count;
        if (free >= candidates.Count)
        {
            chosen.UnionWith(candidates);
        }
        else if (free > 0)
        {
            // Gleichmäßig verteilt, erstes und letztes Foto eingeschlossen.
            for (var k = 0; k < free; k++)
            {
                var position = free == 1 ? 0 : (int)Math.Round(k * (candidates.Count - 1) / (double)(free - 1));
                chosen.Add(candidates[position]);
            }
        }
        return chosen.ToList();
    }

    public static int Distance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);

    /// <summary>
    /// Fotos in Aufnahmereihenfolge: Haben alle Dateinamen eine Kamera-Bildnummer (IMG_1234, DSC01234 …),
    /// wird danach sortiert, sonst bleibt die Reihenfolge des Exports.
    /// </summary>
    public static List<ImageItem> CaptureOrder(IEnumerable<ImageItem> images)
    {
        var ordered = images.OrderBy(i => i.Order).ToList();
        var numbers = ordered.Select(i => CameraNumber(i.FileName)).ToList();
        if (numbers.Any(n => n is null)) return ordered;
        return ordered.Zip(numbers).OrderBy(x => x.Second).Select(x => x.First).ToList();
    }

    private static int? CameraNumber(string fileName)
    {
        var match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"^(?:IMG|DSC|DSCF|PXL|DJI)[_-]?(\d{3,})", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : null;
    }

    /// <summary>
    /// Fingerabdruck für ein Bild: auf 9×8 Pixel verkleinert, in Graustufen; jedes Bit sagt, ob ein Pixel
    /// heller ist als sein rechter Nachbar. Wird im Projekt gespeichert und nur einmal berechnet.
    /// </summary>
    public static ulong ComputeFingerprint(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.DecodePixelWidth = 64; // schnelles Dekodieren in kleiner Größe
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.EndInit();

        var small = new TransformedBitmap(bitmap, new ScaleTransform(9.0 / bitmap.PixelWidth, 8.0 / bitmap.PixelHeight));
        var gray = new FormatConvertedBitmap(small, PixelFormats.Gray8, null, 0);
        var width = gray.PixelWidth;
        var height = gray.PixelHeight;
        var pixels = new byte[width * height];
        gray.CopyPixels(pixels, width, 0);

        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < Math.Min(height, 8); y++)
            for (var x = 0; x < Math.Min(width - 1, 8); x++, bit++)
                if (pixels[y * width + x] > pixels[y * width + x + 1])
                    hash |= 1UL << bit;
        return hash;
    }

    /// <summary>
    /// Plant die Fotoauswahl eines Eintrags; das Titelbild bleibt dabei. Fingerabdrücke werden bei Bedarf
    /// berechnet und am Bild gespeichert. Darf im Hintergrund laufen – setzt selbst keine Auswahl
    /// (die Oberfläche ist an ImageItem.Selected gebunden), dafür <see cref="Apply"/> verwenden.
    /// </summary>
    public static IReadOnlyList<ImageItem> Plan(EntryModel entry, int max, Action<string>? log = null)
    {
        var images = CaptureOrder(entry.Images);
        var available = new List<(ImageItem Image, PhotoInfo Info)>();
        foreach (var image in images)
        {
            if (!File.Exists(image.FullPath)) continue;
            try
            {
                if (!ulong.TryParse(image.Fingerprint, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var fingerprint))
                {
                    fingerprint = ComputeFingerprint(image.FullPath);
                    image.Fingerprint = fingerprint.ToString("x16", CultureInfo.InvariantCulture);
                }
                available.Add((image, new PhotoInfo(fingerprint, new FileInfo(image.FullPath).Length)));
            }
            catch (Exception ex)
            {
                log?.Invoke($"Fingerabdruck nicht möglich: {image.FullPath}: {ex.Message}");
            }
        }

        var hero = available.FindIndex(a => a.Image.IsHero);
        return Choose(available.Select(a => a.Info).ToList(), max, hero >= 0 ? new[] { hero } : null)
            .Select(i => available[i].Image)
            .ToList();
    }

    /// <summary>Plant die Auswahl für mehrere Einträge auf einem eigenen STA-Thread (WPF-Bildklassen).</summary>
    public static Task<List<(EntryModel Entry, IReadOnlyList<ImageItem> Selected)>> PlanAllAsync(
        IReadOnlyList<EntryModel> entries, int max, IProgress<string>? progress = null)
    {
        var tcs = new TaskCompletionSource<List<(EntryModel, IReadOnlyList<ImageItem>)>>();
        var thread = new Thread(() =>
        {
            try
            {
                var plans = new List<(EntryModel, IReadOnlyList<ImageItem>)>();
                for (var i = 0; i < entries.Count; i++)
                {
                    progress?.Report($"Fotos auswählen: Eintrag {i + 1} von {entries.Count}");
                    plans.Add((entries[i], Plan(entries[i], max)));
                }
                tcs.SetResult(plans);
            }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <summary>Setzt die geplante Auswahl und sortiert die Fotos in Aufnahmereihenfolge (auch im Buch).</summary>
    public static void Apply(EntryModel entry, IReadOnlyCollection<ImageItem> selected)
    {
        var order = 1;
        foreach (var image in CaptureOrder(entry.Images))
        {
            image.Order = order++;
            image.Selected = selected.Contains(image);
        }
    }
}
