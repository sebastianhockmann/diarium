using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DiariumBookStudio.Models;
using DiariumBookStudio.Services;

namespace DiariumBookStudio;

/// <summary>Assistent „Jahresbuch drucken“: sammelt die Einstellungen, ausgeführt wird in MainWindow.</summary>
public partial class YearbookWindow : Window
{
    private readonly BookProject _project;
    private string _suggestedTitle = string.Empty;

    public int Year => (int)YearBox.SelectedItem;
    public string BookTitle => TitleBox.Text.Trim();
    public string BookSubtitle => SubtitleBox.Text.Trim();
    public PrintProfile Profile => (PrintProfile)ProfileBox.SelectedItem;
    public bool AutoPhotos => AutoPhotosCheckBox.IsChecked == true;
    public int MaxPhotos => int.TryParse(MaxPhotosBox.Text, out var max) && max > 0 ? max : _project.MaxPhotosPerEntry;
    public bool MarkDone => MarkDoneCheckBox.IsChecked == true;

    public YearbookWindow(BookProject project)
    {
        InitializeComponent();
        _project = project;

        var years = project.AvailableYears;
        YearBox.ItemsSource = years;
        // Vorschlag: das letzte abgeschlossene Jahr, sonst das gewählte bzw. jüngste.
        var lastYear = DateTime.Now.Year - 1;
        YearBox.SelectedItem = years.Contains(lastYear) ? lastYear
            : project.Year is { } year && years.Contains(year) ? year
            : years.LastOrDefault();

        SubtitleBox.Text = project.BookSubtitle;
        ProfileBox.ItemsSource = PrintProfiles.All;
        ProfileBox.SelectedItem = PrintProfiles.Find(project.PrintProfileId);
        MaxPhotosBox.Text = project.MaxPhotosPerEntry.ToString();
    }

    private void YearBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (YearBox.SelectedItem is not int year) return;
        var entries = _project.Entries.Where(en => en.Date?.Year == year).ToList();
        var open = entries.Count(en => !en.Done);
        var photos = entries.Sum(en => en.Images.Count);
        YearInfoText.Text = $"{entries.Count} Einträge mit {photos} Fotos, davon {open} noch nicht geprüft.";

        // Titel nur ersetzen, solange er nicht von Hand geändert wurde.
        var current = TitleBox.Text.Trim();
        var keepProjectTitle = _project.Year == year && !string.IsNullOrWhiteSpace(_project.BookTitle);
        var suggestion = keepProjectTitle ? _project.BookTitle : $"Tagebuch {year}";
        if (current.Length == 0 || current == _suggestedTitle)
            TitleBox.Text = suggestion;
        _suggestedTitle = suggestion;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (YearBox.SelectedItem is not int)
        {
            MessageBox.Show(this, "Das Projekt enthält keine Einträge mit Datum.", "Jahresbuch", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (BookTitle.Length == 0)
        {
            MessageBox.Show(this, "Bitte einen Titel eingeben.", "Jahresbuch", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
