using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DiariumBookStudio;

/// <summary>
/// Lädt ein Bild als kleine Vorschau. Ohne DecodePixelWidth würde WPF jedes Foto in voller
/// Auflösung dekodieren – bei Einträgen mit über 100 Fotos sind das mehr als 1 GB Speicher.
/// OnLoad sorgt außerdem dafür, dass die Datei nicht gesperrt bleibt.
/// </summary>
public sealed class ThumbnailConverter : IValueConverter
{
    public int DecodePixelWidth { get; set; } = 240;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.DecodePixelWidth = DecodePixelWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
