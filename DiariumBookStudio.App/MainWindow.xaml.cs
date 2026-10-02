using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using DiariumBookStudio.Models;
using DiariumBookStudio.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DiariumBookStudio;

public partial class MainWindow : Window
{
    public string AppVersion { get; } = "DiariumBookStudio v21";

    private BookProject? _project;
    private ObservableCollection<MonthGroup> _visibleMonths = new();
    private bool _updatingUi;
    private EntryModel? _currentEntry;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        EntryTree.ItemsSource = _visibleMonths;
    }

    private void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Title = "Diarium-ZIP auswählen",
            Filter = "ZIP-Dateien (*.zip)|*.zip|Alle Dateien (*.*)|*.*"
        };
        if (openDialog.ShowDialog() != true) return;

        var projectRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DiariumBookStudio",
            Path.GetFileNameWithoutExtension(openDialog.FileName));

        if (File.Exists(Path.Combine(projectRoot, "diarium-book-project.json")))
        {
            var answer = MessageBox.Show(this,
                $"Für diese ZIP gibt es bereits ein Projekt:\n{projectRoot}\n\n" +
                "Die Einträge werden neu eingelesen. Bearbeitete Texte, Prüfstatus, Bildauswahl, " +
                "Bildunterschriften und Ortsnamen werden übernommen.\n\nFortfahren?",
                "Erneut importieren", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;
        }

        try
        {
            _project = DiariumImporter.ImportZip(openDialog.FileName, projectRoot);
            ShowProjectSettings();
            RefreshEntryTree();
            StatusText.Text = $"Import fertig: {_project.Entries.Count} Einträge. Projekt: {_project.ProjectFile}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Title = "DiariumBookStudio-Projekt öffnen",
            Filter = "Projektdatei (diarium-book-project.json)|diarium-book-project.json|JSON (*.json)|*.json|Alle Dateien (*.*)|*.*"
        };
        if (openDialog.ShowDialog() != true) return;

        try
        {
            _project = BookProject.Load(openDialog.FileName);
            ShowProjectSettings();
            RefreshEntryTree();
            StatusText.Text = $"Projekt geöffnet: {_project.ProjectFile}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Öffnen fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        _project.Save();
        StatusText.Text = $"Projekt gespeichert: {_project.ProjectFile}";
    }

    private async void ExportLatex_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }

        var defaultOutputDir = Path.Combine(_project.ProjectRoot, "output");
        Directory.CreateDirectory(defaultOutputDir);

        var saveDialog = new SaveFileDialog
        {
            Title = "LaTeX-Datei speichern",
            Filter = "LaTeX-Datei (*.tex)|*.tex|Alle Dateien (*.*)|*.*",
            FileName = "book.tex",
            InitialDirectory = defaultOutputDir,
            AddExtension = true,
            DefaultExt = ".tex"
        };
        if (saveDialog.ShowDialog(this) != true) return;

        try
        {
            SetBuildProgress(true, "Erzeuge LaTeX-Datei und Karten...");
            var texPath = await LatexExporter.ExportAsync(_project, saveDialog.FileName, CreateProgress());
            _project.Save();
            ShowEntry(_currentEntry);
            SetBuildProgress(false, $"LaTeX erzeugt: {texPath}");
            MessageBox.Show(this, $"LaTeX wurde erzeugt:\n{texPath}\n\nMit LuaLaTeX kompilieren.", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "LaTeX-Export fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "LaTeX-Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void GeneratePdf_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }

        var outputDir = Path.Combine(_project.ProjectRoot, "output");
        Directory.CreateDirectory(outputDir);
        var texPath = Path.Combine(outputDir, "book.tex");

        // PDF-Viewer sperren unter Windows oft die geöffnete PDF-Datei.
        // Deshalb bekommt jeder PDF-Lauf einen eigenen Dateinamen.
        var jobName = $"book_{DateTime.Now:yyyyMMdd_HHmmss}";
        var pdfPath = Path.Combine(outputDir, $"{jobName}.pdf");

        try
        {
            SetBuildProgress(true, "Erzeuge LaTeX-Datei und Karten...");
            await LatexExporter.ExportAsync(_project, texPath, CreateProgress());
            _project.Save();
            ShowEntry(_currentEntry);

            SetBuildProgress(true, "Starte LuaLaTeX, Lauf 1 von 2...");
            await RunLuaLatexAsync(texPath, jobName);

            SetBuildProgress(true, "Starte LuaLaTeX, Lauf 2 von 2...");
            await RunLuaLatexAsync(texPath, jobName);

            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("LuaLaTeX wurde ausgeführt, aber die PDF-Datei wurde nicht erzeugt.", pdfPath);

            SetBuildProgress(false, $"PDF erzeugt: {pdfPath}");
            OpenFile(pdfPath);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "PDF-Erzeugung fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "PDF-Erzeugung fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }

        var outputDir = Path.Combine(_project.ProjectRoot, "output");
        Directory.CreateDirectory(outputDir);
        OpenFile(outputDir);
    }

    private static async Task RunLuaLatexAsync(string texPath, string jobName)
    {
        var workingDirectory = Path.GetDirectoryName(texPath)
            ?? throw new InvalidOperationException("Der Ausgabeordner konnte nicht bestimmt werden.");

        var fileName = Path.GetFileName(texPath);

        var psi = new ProcessStartInfo
        {
            FileName = "lualatex",
            Arguments = $"-interaction=nonstopmode -halt-on-error -jobname=\"{jobName}\" \"{fileName}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("LuaLaTeX konnte nicht gestartet werden. Ist MiKTeX installiert und lualatex im PATH?");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var logPath = Path.Combine(workingDirectory, "lualatex_error.txt");
            await File.WriteAllTextAsync(logPath, stdout + Environment.NewLine + stderr, Encoding.UTF8);
            throw new InvalidOperationException(
                "LuaLaTeX hat einen Fehler gemeldet. Details stehen in lualatex_error.txt im Ausgabeordner. Falls dort steht, dass eine PDF nicht geschrieben werden kann, ist sie wahrscheinlich noch in einem PDF-Viewer geöffnet.");
        }
    }

    private IProgress<string> CreateProgress() => new Progress<string>(message => StatusText.Text = message);

    private void SetBuildProgress(bool isRunning, string message)
    {
        StatusText.Text = message;
        BuildProgressBar.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Input.Mouse.OverrideCursor = isRunning ? System.Windows.Input.Cursors.Wait : null;
    }

    private static void OpenFile(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshEntryTree()
    {
        _visibleMonths.Clear();
        _currentEntry = null;
        ShowEntry(null);
        if (_project is null) return;

        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var entries = _project.Entries
            .Where(entry => string.IsNullOrWhiteSpace(query)
                || entry.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.EditedText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.OriginalText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Date ?? DateTime.MaxValue)
            .ToList();

        foreach (var group in entries.GroupBy(GetMonthKey))
        {
            var month = new MonthGroup { Title = group.Key };
            foreach (var entry in group) month.Entries.Add(entry);
            _visibleMonths.Add(month);
        }

        ExpandAllTreeItems();
    }

    private static string GetMonthKey(EntryModel entry)
        => entry.Date?.ToString("MMMM yyyy", new CultureInfo("de-DE")) ?? "Ohne Datum";

    private void ExpandAllTreeItems()
    {
        EntryTree.UpdateLayout();
        foreach (var item in EntryTree.Items)
        {
            if (EntryTree.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeItem)
                treeItem.IsExpanded = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshEntryTree();

    private EntryModel? CurrentEntry => _currentEntry;

    private void EntryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is EntryModel entry)
        {
            _currentEntry = entry;
            ShowEntry(entry);
        }
    }

    private void ShowEntry(EntryModel? entry)
    {
        _updatingUi = true;
        try
        {
            if (entry is null)
            {
                EntryTitle.Text = "Kein Eintrag ausgewählt";
                EntryMeta.Text = string.Empty;
                EditedTextBox.Text = string.Empty;
                PlaceNameBox.Text = string.Empty;
                ImagesControl.ItemsSource = null;
                ImageCountText.Text = string.Empty;
                DoneCheckBox.IsChecked = false;
                return;
            }

            EntryTitle.Text = entry.DisplayTitle;
            EntryMeta.Text = BuildEntryMeta(entry);
            EditedTextBox.Text = entry.EditedText;
            PlaceNameBox.Text = entry.PlaceName;
            DoneCheckBox.IsChecked = entry.Done;
            ImagesControl.ItemsSource = entry.Images.OrderBy(i => i.Order).ToList();
            ImageCountText.Text = $"{entry.Images.Count(i => i.Selected)} von {entry.Images.Count} ausgewählt";
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void EditedTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi) return;
        if (CurrentEntry is { } entry) entry.EditedText = EditedTextBox.Text;
    }

    private void PlaceNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi) return;
        if (CurrentEntry is { } entry) entry.PlaceName = PlaceNameBox.Text;
    }

    private void ShowProjectSettings()
    {
        _updatingUi = true;
        try
        {
            BookTitleBox.Text = _project?.BookTitle ?? string.Empty;
            BookSubtitleBox.Text = _project?.BookSubtitle ?? string.Empty;
            PrintVersionCheckBox.IsChecked = _project?.PrintVersion == true;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void BookSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || _project is null) return;
        _project.BookTitle = BookTitleBox.Text;
        _project.BookSubtitle = BookSubtitleBox.Text;
        _project.PrintVersion = PrintVersionCheckBox.IsChecked == true;
    }

    /// <summary>Pro Eintrag gibt es höchstens ein Titelbild.</summary>
    private void HeroCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageItem image || CurrentEntry is not { } entry) return;
        foreach (var other in entry.Images.Where(i => !ReferenceEquals(i, image)))
            other.IsHero = false;
        image.Selected = true;
    }

    private void DoneCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi) return;
        if (CurrentEntry is { } entry)
        {
            entry.Done = DoneCheckBox.IsChecked == true;
            StatusText.Text = entry.Done ? "Eintrag als geprüft markiert." : "Eintrag als nicht geprüft markiert.";
        }
    }

    private void SelectAllImages_Click(object sender, RoutedEventArgs e) => SetImageSelection(_ => true);
    private void SelectNoImages_Click(object sender, RoutedEventArgs e) => SetImageSelection(_ => false);
    private void SelectFirstSix_Click(object sender, RoutedEventArgs e) => SetImageSelection(i => i.Order <= 6);

    private void SetImageSelection(Func<ImageItem, bool> selector)
    {
        if (CurrentEntry is not { } entry) return;
        foreach (var image in entry.Images) image.Selected = selector(image);
        ShowEntry(entry);
    }

    private void MoveImageUp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageItem image || CurrentEntry is not { } entry) return;
        var ordered = entry.Images.OrderBy(i => i.Order).ToList();
        var index = ordered.IndexOf(image);
        if (index <= 0) return;
        (ordered[index - 1].Order, ordered[index].Order) = (ordered[index].Order, ordered[index - 1].Order);
        ShowEntry(entry);
    }

    private void MoveImageDown_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageItem image || CurrentEntry is not { } entry) return;
        var ordered = entry.Images.OrderBy(i => i.Order).ToList();
        var index = ordered.IndexOf(image);
        if (index < 0 || index >= ordered.Count - 1) return;
        (ordered[index + 1].Order, ordered[index].Order) = (ordered[index].Order, ordered[index + 1].Order);
        ShowEntry(entry);
    }
private static string BuildEntryMeta(EntryModel entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Location))
            parts.Add(entry.Location);
        if (!string.IsNullOrWhiteSpace(entry.Weather))
            parts.Add(entry.Weather.StartsWith("Wetter", StringComparison.OrdinalIgnoreCase) ? entry.Weather : $"Wetter: {entry.Weather}");
        return string.Join("   ", parts);
    }

}
