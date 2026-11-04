using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using DiariumBookStudio.Models;

namespace DiariumBookStudio.Services;

/// <summary>Ein Foto mit Maßen nach EXIF-Drehung – noch nicht in den Ausgabeordner geschrieben.</summary>
public sealed record SourceImage(ImageItem Item, int Rotation, double PixelWidth, double PixelHeight)
{
    public double Aspect => PixelWidth / PixelHeight;
}

/// <summary>
/// Bereitet Fotos für den Druck vor: liest Maße und EXIF-Drehung (nur Dateikopf, schnell) und schreibt
/// die Datei passend zur Druckgröße in den images-Ordner. Fotos über 600 ppi werden auf 450 ppi
/// verkleinert (Lulu: höchstens 600 ppi; spart außerdem viel PDF-Größe). Ergebnisse werden
/// zwischengespeichert und nur neu berechnet, wenn sich das Original ändert.
/// </summary>
public static class ImageExport
{
    public const double MaxPpi = 600;
    public const double TargetPpi = 450;

    private static readonly HashSet<string> LatexImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

    public static SourceImage? Read(ImageItem image, EntryModel? entry, Action<string> log, PreflightReport? report)
    {
        if (!File.Exists(image.FullPath))
        {
            log($"Bild fehlt: {image.FullPath}");
            report?.Add(PreflightSeverity.Fehler, entry, $"Foto {image.FileName} fehlt und wird nicht gedruckt: {image.FullPath}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(image.FullPath);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None).Frames[0];
            var rotation = ReadExifRotation(frame);
            var swap = rotation is 90 or 270;
            return new SourceImage(image, rotation,
                swap ? frame.PixelHeight : frame.PixelWidth,
                swap ? frame.PixelWidth : frame.PixelHeight);
        }
        catch (Exception ex)
        {
            log($"Bild konnte nicht verarbeitet werden: {image.FullPath}: {ex.Message}");
            report?.Add(PreflightSeverity.Fehler, entry, $"Foto {image.FileName} konnte nicht gelesen werden und wird nicht gedruckt: {ex.Message}");
            return null;
        }
    }

    /// <summary>Stabiler, LaTeX-tauglicher Dateiname (ohne Endung) für ein Foto eines Eintrags.</summary>
    public static string BaseName(string prefix, ImageItem image)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(image.RelativePath));
        return $"{prefix}_{Convert.ToHexString(hash, 0, 4).ToLowerInvariant()}";
    }

    /// <summary>
    /// Schreibt das Foto für eine Druckbreite von <paramref name="printWidthMm"/> in den images-Ordner
    /// und gibt den Pfad relativ zur .tex-Datei zurück.
    /// </summary>
    public static string Write(SourceImage image, string imageDir, string baseName, double printWidthMm)
    {
        var source = image.Item.FullPath;
        var ppi = printWidthMm > 0 ? image.PixelWidth / (printWidthMm / 25.4) : 0;
        var targetWidth = ppi > MaxPpi
            ? (int)Math.Round(printWidthMm / 25.4 * TargetPpi)
            : (int)image.PixelWidth;
        var scale = targetWidth < image.PixelWidth;
        var ext = Path.GetExtension(source).ToLowerInvariant();
        var reencode = scale || image.Rotation != 0 || !LatexImageExtensions.Contains(ext);

        var fileName = reencode ? $"{baseName}_{targetWidth}.jpg" : baseName + ext;
        var target = Path.Combine(imageDir, fileName);

        if (reencode)
        {
            if (!File.Exists(target) || File.GetLastWriteTimeUtc(target) < File.GetLastWriteTimeUtc(source))
                Encode(image, scale ? targetWidth : 0, target);
        }
        else if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(source).Length)
        {
            File.Copy(source, target, true);
        }
        return "images/" + fileName;
    }

    /// <param name="targetWidth">Breite nach Drehung in Pixeln, 0 = Originalgröße.</param>
    private static void Encode(SourceImage image, int targetWidth, string target)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(image.Item.FullPath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (targetWidth > 0)
        {
            // Die Decodergröße bezieht sich auf das gespeicherte (ungedrehte) Bild.
            if (image.Rotation is 90 or 270) bitmap.DecodePixelHeight = targetWidth;
            else bitmap.DecodePixelWidth = targetWidth;
        }
        bitmap.EndInit();

        BitmapSource result = image.Rotation != 0
            ? new TransformedBitmap(bitmap, new System.Windows.Media.RotateTransform(image.Rotation))
            : bitmap;
        var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
        encoder.Frames.Add(BitmapFrame.Create(result));

        // Erst vollständig schreiben, dann umbenennen: ein abgebrochener Export hinterlässt keine halbe Datei im Cache.
        var temp = target + ".tmp";
        using (var output = File.Create(temp))
            encoder.Save(output);
        File.Move(temp, target, overwrite: true);
    }

    private static int ReadExifRotation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
                return orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
        }
        catch (NotSupportedException) { }
        return 0;
    }
}
