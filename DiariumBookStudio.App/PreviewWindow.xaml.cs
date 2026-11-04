using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiariumBookStudio.Models;
using DiariumBookStudio.Services;

namespace DiariumBookStudio;

/// <summary>Zeigt die Vorschau als Doppelseiten. Erzeugt wird sie von MainWindow (eigener Export-Lauf).</summary>
public partial class PreviewWindow : Window
{
    public static readonly DependencyProperty PageWidthProperty =
        DependencyProperty.Register(nameof(PageWidth), typeof(double), typeof(PreviewWindow), new PropertyMetadata(440.0));

    /// <summary>Anzeigebreite einer Seite in Pixeln (Zoom).</summary>
    public double PageWidth
    {
        get => (double)GetValue(PageWidthProperty);
        set => SetValue(PageWidthProperty, value);
    }

    private readonly Func<bool, Task<PreviewResult?>> _create;
    private readonly Action<EntryModel> _navigate;
    private PreviewResult? _result;

    /// <param name="create">Erzeugt die Vorschau für den aktuellen Eintrag (true = ganzer Monat).</param>
    /// <param name="navigate">Öffnet einen Eintrag in der Hauptansicht.</param>
    public PreviewWindow(Func<bool, Task<PreviewResult?>> create, Action<EntryModel> navigate)
    {
        InitializeComponent();
        _create = create;
        _navigate = navigate;
    }

    public async Task RefreshAsync()
    {
        BusyText.Visibility = Visibility.Visible;
        SpreadList.ItemsSource = null;
        try
        {
            var result = await _create(MonthScope.IsChecked == true);
            if (result is null) return;
            _result = result;
            Show(result);
        }
        finally
        {
            BusyText.Visibility = Visibility.Collapsed;
        }
    }

    private void Show(PreviewResult result)
    {
        OpenPdfButton.IsEnabled = true;
        TitleText.Text = result.WholeMonth ? $"Vorschau {result.Title}" : $"Vorschau: {result.Title}";
        var perPage = result.EntryCount / (double)result.ContentPages;
        var pagesWord = result.ContentPages == 1 ? "Seite" : "Seiten";
        SummaryText.Text = result.WholeMonth
            ? $"{result.EntryCount} Einträge mit {result.PhotoCount} Fotos auf {result.ContentPages} {pagesWord} (plus Monatsseite) – " +
              $"im Schnitt {perPage.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} Einträge je Seite " +
              $"bzw. {(1 / perPage).ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} Seiten je Eintrag."
            : $"{result.ContentPages} {pagesWord} mit {result.PhotoCount} Fotos.";
        SummaryText.Text += " Seitenzahlen beginnen in der Vorschau bei 1; im Buch kann ein Eintrag auf einer linken oder rechten Seite beginnen.";

        // Seite 1 ist eine rechte Seite – wie im aufgeschlagenen Buch.
        var pages = result.Pages.Select(p => new PageView(p)).ToList();
        var spreads = new List<Spread> { new(null, pages.FirstOrDefault()) };
        for (var i = 1; i < pages.Count; i += 2)
            spreads.Add(new Spread(pages[i], i + 1 < pages.Count ? pages[i + 1] : null));
        SpreadList.ItemsSource = spreads;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_result is not null)
            Process.Start(new ProcessStartInfo(_result.PdfPath) { UseShellExecute = true });
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PageWidth = e.NewValue;

    private void Page_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || (sender as FrameworkElement)?.Tag is not PageView page) return;
        if ((page.Page.StartingEntries.FirstOrDefault() ?? page.Page.ContinuedEntry) is { } entry)
            _navigate(entry);
    }

    /// <summary>Eine Doppelseite; leere Hälften sind null.</summary>
    public sealed record Spread(PageView? Left, PageView? Right);

    public sealed class PageView
    {
        public PageView(PreviewPage page)
        {
            Page = page;
            // OnLoad: die Datei wird sofort freigegeben, damit die nächste Vorschau sie überschreiben kann.
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(page.ImagePath);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Image = image;

            var titles = page.StartingEntries.Select(ShortTitle).ToList();
            Caption = titles.Count > 0
                ? $"Seite {page.Number}: {string.Join(" · ", titles)}"
                : page.ContinuedEntry is { } running
                    ? $"Seite {page.Number}: Fortsetzung {ShortTitle(running)}"
                    : $"Seite {page.Number}: Monatsseite";
        }

        public PreviewPage Page { get; }
        public ImageSource Image { get; }
        public string Caption { get; }
        public TextAlignment CaptionAlignment => Page.Number % 2 == 0 ? TextAlignment.Left : TextAlignment.Right;

        private static string ShortTitle(EntryModel entry)
        {
            var date = entry.Date?.ToString("d. MMM", CultureInfo.GetCultureInfo("de-DE")) ?? entry.DateText;
            return string.IsNullOrWhiteSpace(entry.Title) ? date : $"{date} {entry.Title}";
        }
    }
}
