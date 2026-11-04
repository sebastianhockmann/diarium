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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace DiariumBookStudio;

public partial class MainWindow : Window
{
    public string AppVersion { get; } = "DiariumBookStudio v29";

    private BookProject? _project;
    private ObservableCollection<MonthGroup> _visibleMonths = new();
    private bool _updatingUi;
    private EntryModel? _currentEntry;

    // Ungespeicherte Änderungen. Wird auch vom Export-Thread gesetzt (Ortsnamen), daher volatile.
    private volatile bool _dirty;
    private bool _isBuilding;
    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private AppSettings _settings = AppSettings.Load();
    private PreviewWindow? _previewWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        EntryTree.ItemsSource = _visibleMonths;
        Loaded += (_, _) => OpenLastProject();
        _autosaveTimer.Tick += (_, _) =>
        {
            if (_dirty && !_isBuilding) TrySave(automatic: true);
        };
        _autosaveTimer.Start();
    }

    private void OpenLastProject()
    {
        if (!_settings.OpenLastProject || !File.Exists(_settings.LastProjectFile)) return;
        try
        {
            AttachProject(BookProject.Load(_settings.LastProjectFile));
            StatusText.Text = $"Zuletzt geöffnetes Projekt: {_settings.LastProjectFile}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Letztes Projekt konnte nicht geöffnet werden: {ex.Message}";
        }
    }

    /// <summary>Setzt das aktuelle Projekt und beobachtet Änderungen an Einträgen und Bildern fürs Autospeichern.</summary>
    private void AttachProject(BookProject project)
    {
        _project = project;
        _settings.LastProjectFile = project.ProjectFile;
        TrySaveSettings();
        foreach (var entry in project.Entries)
        {
            entry.PropertyChanged += (_, _) => _dirty = true;
            foreach (var image in entry.Images)
                image.PropertyChanged += (_, _) => _dirty = true;
        }
        _dirty = false;
        ShowProjectSettings();
        RefreshEntryTree();
    }

    private bool TrySave(bool automatic)
    {
        if (_project is null) return true;
        _dirty = false;
        try
        {
            _project.Save();
            StatusText.Text = automatic
                ? $"Automatisch gespeichert ({DateTime.Now:HH:mm:ss})."
                : $"Projekt gespeichert: {_project.ProjectFile}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dirty = true;
            if (automatic)
                StatusText.Text = $"Automatisches Speichern fehlgeschlagen: {ex.Message}";
            else
                MessageBox.Show(this, ex.Message, "Speichern fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>Speichert vor dem Wechsel zu einem anderen Projekt. false: Wechsel abbrechen.</summary>
    private bool SaveBeforeSwitch()
    {
        if (!_dirty || TrySave(automatic: true)) return true;
        return MessageBox.Show(this,
            "Das geöffnete Projekt konnte nicht gespeichert werden. Trotzdem fortfahren? Ungespeicherte Änderungen gehen verloren.",
            "Speichern fehlgeschlagen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_dirty || TrySave(automatic: true)) return;
        var answer = MessageBox.Show(this,
            "Das Projekt konnte nicht gespeichert werden. Trotzdem beenden? Ungespeicherte Änderungen gehen verloren.",
            "Speichern fehlgeschlagen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Title = "Diarium-ZIP auswählen",
            Filter = "ZIP-Dateien (*.zip)|*.zip|Alle Dateien (*.*)|*.*"
        };
        if (openDialog.ShowDialog() != true) return;

        RunImport(Path.GetFileNameWithoutExtension(openDialog.FileName),
            root => DiariumImporter.ImportZip(openDialog.FileName, root));
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Entpackten Diarium-Export auswählen (Ordner mit HTML-Datei und media-Ordner)"
        };
        if (folderDialog.ShowDialog(this) != true) return;

        RunImport(new DirectoryInfo(folderDialog.FolderName).Name,
            root => DiariumImporter.ImportFolder(folderDialog.FolderName, root));
    }

    /// <summary>
    /// Ist ein Projekt geöffnet, kann der neue Export in dieses Projekt übernommen werden.
    /// So bleiben Bearbeitungen erhalten, auch wenn der Exportname sich ändert
    /// (z. B. „Diarium_2026-01-01_2026-06-30“ → „Diarium_2026-01-01_2026-12-31“).
    /// </summary>
    private void RunImport(string exportName, Func<string, BookProject> import)
    {
        var projectRoot = Path.Combine(_settings.ProjectsFolder, exportName);

        if (_project is not null)
        {
            var answer = MessageBox.Show(this,
                $"Export in das geöffnete Projekt übernehmen?\n{_project.ProjectRoot}\n\n" +
                "Ja: Einträge werden aktualisiert, Bearbeitungen (Texte, Prüfstatus, Bildauswahl, " +
                "Bildunterschriften, Ortsnamen) bleiben erhalten.\n" +
                $"Nein: Neues Projekt unter\n{projectRoot}",
                "Import", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            if (!SaveBeforeSwitch()) return;
            if (answer == MessageBoxResult.Yes)
                projectRoot = _project.ProjectRoot;
        }

        if (projectRoot != _project?.ProjectRoot && File.Exists(Path.Combine(projectRoot, "diarium-book-project.json")))
        {
            var answer = MessageBox.Show(this,
                $"Für diesen Export gibt es bereits ein Projekt:\n{projectRoot}\n\n" +
                "Die Einträge werden neu eingelesen. Bearbeitete Texte, Prüfstatus, Bildauswahl, " +
                "Bildunterschriften und Ortsnamen werden übernommen.\n\nFortfahren?",
                "Erneut importieren", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;
        }

        try
        {
            var isNew = !File.Exists(Path.Combine(projectRoot, "diarium-book-project.json"));
            var project = import(projectRoot);
            if (isNew)
            {
                // Voreinstellungen aus den Einstellungen übernehmen.
                project.PrintProfileId = _settings.DefaultPrintProfileId;
                project.MaxPhotosPerEntry = _settings.DefaultMaxPhotosPerEntry;
                project.Save();
            }
            AttachProject(project);
            StatusText.Text = $"Import fertig: {project.Entries.Count} Einträge. Projekt: {project.ProjectFile}";
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
        if (!SaveBeforeSwitch()) return;

        try
        {
            var project = BookProject.Load(openDialog.FileName);
            AttachProject(project);
            StatusText.Text = $"Projekt geöffnet: {project.ProjectFile}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Öffnen fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e) => TrySave(automatic: false);

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e) => TrySave(automatic: false);

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
            TrySave(automatic: true);
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

    private async void GeneratePdf_Click(object sender, RoutedEventArgs e) => await BuildBookAsync(package: false);

    /// <summary>Assistent: Jahr und Titel wählen, Fotos automatisch auswählen, Druckpaket erzeugen.</summary>
    private async void Yearbook_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }
        var wizard = new YearbookWindow(_project) { Owner = this };
        if (wizard.ShowDialog() != true) return;

        _project.Year = wizard.Year;
        _project.BookTitle = wizard.BookTitle;
        _project.BookSubtitle = wizard.BookSubtitle;
        _project.PrintProfileId = wizard.Profile.Id;
        _project.MaxPhotosPerEntry = wizard.MaxPhotos;
        _project.PrintVersion = true;
        _dirty = true;
        ShowProjectSettings();
        RefreshEntryTree();

        try
        {
            if (wizard.AutoPhotos)
                await AutoSelectPhotosAsync(_project.BookEntries.Where(en => !en.Done).ToList(), wizard.MaxPhotos);
            if (wizard.MarkDone)
                MarkDone(_project.BookEntries);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "Fotoauswahl fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "Fotoauswahl fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        await BuildBookAsync(package: true);
    }

    private async void AutoPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var open = _project.BookEntries.Where(en => !en.Done).ToList();
        if (open.Count == 0)
        {
            MessageBox.Show(this, "Alle Einträge sind bereits geprüft – deren Fotoauswahl bleibt unverändert.", "Fotos auswählen");
            return;
        }
        var scope = _project.Year is { } year ? $"aus {year} " : string.Empty;
        if (MessageBox.Show(this,
                $"Für {open.Count} ungeprüfte Einträge {scope}die Fotos neu auswählen?\n\n" +
                $"Höchstens {_project.MaxPhotosPerEntry} je Eintrag, Serienbilder werden zusammengefasst, Titelbilder bleiben. " +
                "Die bisherige Auswahl dieser Einträge wird ersetzt; geprüfte Einträge bleiben unverändert.",
                "Fotos automatisch auswählen", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        try
        {
            await AutoSelectPhotosAsync(open, _project.MaxPhotosPerEntry);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "Fotoauswahl fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "Fotoauswahl fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AutoSelectPhotosAsync(IReadOnlyList<EntryModel> entries, int max)
    {
        SetBuildProgress(true, "Fotos werden ausgewählt...");
        var plans = await PhotoSelection.PlanAllAsync(entries, max, CreateProgress());
        foreach (var (entry, selected) in plans)
            PhotoSelection.Apply(entry, selected.ToHashSet());
        _dirty = true; // auch neu berechnete Fingerabdrücke speichern
        ShowEntry(_currentEntry);
        var before = entries.Sum(en => en.Images.Count);
        SetBuildProgress(false, $"Fotos ausgewählt: {plans.Sum(p => p.Selected.Count)} von {before} in {entries.Count} Einträgen.");
    }

    private void MarkAllDone_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null) return;
        var open = _project.BookEntries.Count(en => !en.Done);
        if (open == 0) return;
        if (MessageBox.Show(this, $"{open} Einträge als geprüft markieren?", "Alle als geprüft markieren",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        MarkDone(_project.BookEntries);
    }

    private void MarkDone(IEnumerable<EntryModel> entries)
    {
        var count = 0;
        foreach (var entry in entries.Where(en => !en.Done))
        {
            entry.Done = true;
            count++;
        }
        ShowEntry(_currentEntry);
        StatusText.Text = $"{count} Einträge als geprüft markiert.";
    }

    /// <summary>Druckversion mit Umschlag erzeugen und beides mit Druckinfo in einen Paketordner legen.</summary>
    private async void PrintPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }
        if (!_project.PrintVersion)
        {
            PrintVersionCheckBox.IsChecked = true; // setzt über BookSettings_Changed auch das Projekt
            StatusText.Text = "Druckversion eingeschaltet.";
        }
        await BuildBookAsync(package: true);
    }

    /// <param name="package">true: Ergebnis als Druckpaket (Ordner mit PDFs und Druckinfo) ablegen.</param>
    private async Task BuildBookAsync(bool package)
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
            var report = await ExportWithReportAsync(texPath);
            // Nur bei Fehlern und Warnungen nachfragen; Hinweise stehen in der Statuszeile.
            if (report.Issues.Any(i => i.Severity != PreflightSeverity.Hinweis) && !ShowPreflight(report, allowContinue: true))
            {
                SetBuildProgress(false, $"PDF-Erzeugung abgebrochen. Vorabprüfung: {report.Summary}");
                return;
            }

            var pages = await CompileInteriorAsync(texPath, jobName);
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("LuaLaTeX wurde ausgeführt, aber die PDF-Datei wurde nicht erzeugt.", pdfPath);

            if (!_project.PrintVersion)
            {
                SetBuildProgress(false, $"PDF erzeugt: {pdfPath} ({pages} Seiten) – Vorabprüfung: {report.Summary}");
                OpenFile(pdfPath);
                return;
            }

            // Druckversion: Der Bundsteg hängt von der Seitenzahl ab. Passt er nicht zur geschätzten
            // Seitenzahl, Seitenmaße anpassen und neu setzen (nur beim ersten Lauf oder bei großen Änderungen).
            var used = LatexExporter.GeometryFor(_project);
            var needed = LatexExporter.GeometryFor(_project, pages);
            _project.LastPageCount = pages;
            _dirty = true;
            if (needed != used)
            {
                LatexExporter.WriteGeometry(outputDir, needed);
                pages = await CompileInteriorAsync(texPath, jobName, "Bundsteg an Seitenzahl angepasst, ");
                _project.LastPageCount = pages;
            }

            var profile = PrintProfiles.Find(_project.PrintProfileId);
            SetBuildProgress(true, "Erzeuge Umschlag...");
            var coverReport = new PreflightReport();
            var coverTex = CoverExporter.Export(_project, profile, pages, outputDir, coverReport);
            var coverJob = $"cover_{DateTime.Now:yyyyMMdd_HHmmss}";
            await RunLuaLatexAsync(coverTex, coverJob);
            var coverPath = Path.Combine(outputDir, $"{coverJob}.pdf");
            TrySave(automatic: true);

            var (coverWidth, coverHeight, spine) = profile.CoverSize(pages);
            var problems = new List<(string Symbol, string Message)>();
            if (profile.CheckPageCount(pages) is { } pageProblem)
                problems.Add(("⚠", pageProblem));
            problems.AddRange(coverReport.Issues.Select(i => (i.Severity == PreflightSeverity.Hinweis ? "ℹ" : "⚠", i.Message)));

            string? packageFolder = null;
            if (package)
            {
                var notes = problems.Select(p => p.Message).ToList();
                var notReviewed = report.Issues.Count(i => i.Message.Contains("geprüft"));
                if (notReviewed > 0) notes.Add($"{notReviewed} Einträge sind noch nicht als geprüft markiert.");
                var packageRoot = string.IsNullOrWhiteSpace(_settings.PrintPackageFolder) ? outputDir : _settings.PrintPackageFolder;
                Directory.CreateDirectory(packageRoot);
                packageFolder = PrintPackage.Create(_project, profile, pages, pdfPath, coverPath, packageRoot, notes);
            }

            var summary = new StringBuilder()
                .AppendLine(package ? $"Druckpaket für {profile.Name}:" : $"Druckdateien für {profile.Name}:")
                .AppendLine()
                .AppendLine($"Innenteil: {pages} Seiten")
                .AppendLine($"Umschlag: {coverWidth:0.0} × {coverHeight:0.0} mm, Buchrücken {spine:0.0} mm");
            if (packageFolder is not null)
                summary.AppendLine().AppendLine($"Ordner: {packageFolder}").AppendLine("Die Schritte zum Bestellen stehen in „Druckinfo.txt“.");
            foreach (var (symbol, message) in problems)
                summary.AppendLine().AppendLine($"{symbol} {message}");

            SetBuildProgress(false, $"{(package ? "Druckpaket" : "Druckdateien")} erzeugt: {pages} Seiten, Umschlag {coverWidth:0.0} × {coverHeight:0.0} mm – Vorabprüfung: {report.Summary}");
            if (packageFolder is not null)
            {
                OpenFile(packageFolder);
            }
            else
            {
                OpenFile(pdfPath);
                OpenFile(coverPath);
            }
            MessageBox.Show(this, summary.ToString(), package ? "Druckpaket erzeugt" : "Druckdateien erzeugt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "PDF-Erzeugung fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "PDF-Erzeugung fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Erzeugt book.tex ohne LuaLaTeX und zeigt alle Auffälligkeiten an.</summary>
    private async void Preflight_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }

        var outputDir = Path.Combine(_project.ProjectRoot, "output");
        Directory.CreateDirectory(outputDir);
        try
        {
            var report = await ExportWithReportAsync(Path.Combine(outputDir, "book.tex"));
            ShowPreflight(report, allowContinue: false);
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "Vorabprüfung fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "Vorabprüfung fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<PreflightReport> ExportWithReportAsync(string texPath)
    {
        var report = new PreflightReport();
        SetBuildProgress(true, "Erzeuge LaTeX-Datei und Karten...");
        await LatexExporter.ExportAsync(_project!, texPath, CreateProgress(), report);
        TrySave(automatic: true);
        ShowEntry(_currentEntry);
        return report;
    }

    /// <summary>Zeigt die Vorabprüfung. true: PDF trotzdem erzeugen.</summary>
    private bool ShowPreflight(PreflightReport report, bool allowContinue)
    {
        SetBuildProgress(false, $"Vorabprüfung: {report.Summary}");
        var window = new PreflightWindow(report, allowContinue) { Owner = this };
        var proceed = window.ShowDialog() == true;
        if (window.SelectedEntry is { } entry) SelectEntry(entry);
        return proceed;
    }

    /// <summary>Wählt einen Eintrag im Baum aus (z. B. aus der Vorabprüfung).</summary>
    private void SelectEntry(EntryModel entry)
    {
        if (!_visibleMonths.Any(m => m.Entries.Contains(entry)))
            SearchBox.Text = string.Empty; // Suchfilter aufheben, baut den Baum neu auf
        var month = _visibleMonths.FirstOrDefault(m => m.Entries.Contains(entry));
        if (month is null) return;

        EntryTree.UpdateLayout();
        if (EntryTree.ItemContainerGenerator.ContainerFromItem(month) is TreeViewItem monthItem)
        {
            monthItem.IsExpanded = true;
            monthItem.UpdateLayout();
            if (monthItem.ItemContainerGenerator.ContainerFromItem(entry) is TreeViewItem entryItem)
            {
                entryItem.IsSelected = true;
                entryItem.BringIntoView();
                return;
            }
        }
        _currentEntry = entry;
        ShowEntry(entry);
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

    /// <summary>Zwei LuaLaTeX-Läufe (Inhaltsverzeichnis), liefert die Seitenzahl.</summary>
    private async Task<int> CompileInteriorAsync(string texPath, string jobName, string prefix = "")
    {
        SetBuildProgress(true, $"{prefix}LuaLaTeX, Lauf 1 von 2...");
        await RunLuaLatexAsync(texPath, jobName);
        SetBuildProgress(true, $"{prefix}LuaLaTeX, Lauf 2 von 2...");
        return LatexRunner.PageCount(await RunLuaLatexAsync(texPath, jobName));
    }

    /// <returns>Ausgabe von LuaLaTeX.</returns>
    private Task<string> RunLuaLatexAsync(string texPath, string jobName)
        => LatexRunner.RunAsync(_settings.LuaLatexCommand, texPath, jobName);
    private IProgress<string> CreateProgress() => new Progress<string>(message => StatusText.Text = message);

    private void SetBuildProgress(bool isRunning, string message)
    {
        // Während des Exports kein Autospeichern: der Export-Thread ergänzt gerade Ortsnamen.
        _isBuilding = isRunning;
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

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true) return;
        _settings = window.Settings;
        if (TrySaveSettings())
            StatusText.Text = "Einstellungen gespeichert.";
    }

    private bool TrySaveSettings()
    {
        try
        {
            _settings.Save();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Einstellungen konnten nicht gespeichert werden: {ex.Message}";
            return false;
        }
    }

    private void Help_Executed(object sender, ExecutedRoutedEventArgs e) => new HelpWindow { Owner = this }.Show();

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Diarium-ZIP importieren oder ein Projekt öffnen.");
            return;
        }
        if (_previewWindow is null)
        {
            _previewWindow = new PreviewWindow(CreatePreviewAsync, SelectEntry) { Owner = this };
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.Show();
        }
        else
        {
            _previewWindow.Activate();
        }
        await _previewWindow.RefreshAsync();
    }

    /// <summary>Vorschau für den ausgewählten Eintrag bzw. seinen Monat (für das Vorschaufenster).</summary>
    private async Task<PreviewResult?> CreatePreviewAsync(bool wholeMonth)
    {
        if (_project is null) return null;
        var entry = _currentEntry ?? _project.BookEntries.OrderBy(en => en.Date ?? DateTime.MaxValue).FirstOrDefault();
        if (entry is null)
        {
            MessageBox.Show(this, "Das Buch enthält keine Einträge.", "Vorschau");
            return null;
        }
        if (_isBuilding)
        {
            MessageBox.Show(this, "Es läuft gerade schon ein Export. Bitte kurz warten.", "Vorschau");
            return null;
        }

        try
        {
            SetBuildProgress(true, "Vorschau wird erzeugt...");
            var result = await PreviewService.CreateAsync(_project, entry, wholeMonth, _settings, CreateProgress());
            ShowEntry(_currentEntry); // Ortsnamen können beim Export ergänzt worden sein
            SetBuildProgress(false, $"Vorschau: {result.Title} – {result.Pages.Count} Seiten.");
            return result;
        }
        catch (Exception ex)
        {
            SetBuildProgress(false, "Vorschau fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "Vorschau fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show(this,
            $"{AppVersion}\n\nMacht aus einem Diarium-Export ein gedrucktes Buch.\n\n" +
            $"Einstellungen: %APPDATA%\\DiariumBookStudio\\settings.json\nProjektordner: {_settings.ProjectsFolder}",
            "Über Diarium Book Studio", MessageBoxButton.OK, MessageBoxImage.Information);

    private void RefreshEntryTree()
    {
        _visibleMonths.Clear();
        _currentEntry = null;
        ShowEntry(null);
        if (_project is null) return;

        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var entries = _project.BookEntries
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
            GrayAccentsCheckBox.IsChecked = _project?.GrayAccents == true;
            PrintProfileComboBox.ItemsSource = PrintProfiles.All;
            PrintProfileComboBox.SelectedItem = _project is null ? null : PrintProfiles.Find(_project.PrintProfileId);
            UpdateCoverImageText();

            // Erster Eintrag „Alle Jahre“ (null), danach die vorhandenen Jahre.
            var years = new List<object> { AllYears };
            years.AddRange((_project?.AvailableYears ?? Array.Empty<int>()).Cast<object>());
            YearComboBox.ItemsSource = years;
            YearComboBox.SelectedItem = _project?.Year is { } year && years.Contains(year) ? year : AllYears;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private const string AllYears = "Alle";

    private void BookSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || _project is null) return;
        _project.BookTitle = BookTitleBox.Text;
        _project.BookSubtitle = BookSubtitleBox.Text;
        _project.PrintVersion = PrintVersionCheckBox.IsChecked == true;
        _project.GrayAccents = GrayAccentsCheckBox.IsChecked == true;
        if (PrintProfileComboBox.SelectedItem is PrintProfile profile)
            _project.PrintProfileId = profile.Id;
        _dirty = true;
    }

    private void CoverImage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageItem image || _project is null) return;
        _project.CoverImage = image.RelativePath;
        _dirty = true;
        UpdateCoverImageText();
        StatusText.Text = $"Umschlagfoto: {image.FileName}";
    }

    private void UpdateCoverImageText()
        => CoverImageText.Text = _project is null ? string.Empty
            : string.IsNullOrEmpty(_project.CoverImage) ? "Umschlagfoto: automatisch"
            : $"Umschlagfoto: {Path.GetFileName(_project.CoverImage)}";

    private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || _project is null) return;
        _project.Year = YearComboBox.SelectedItem as int?;
        _dirty = true;
        RefreshEntryTree();
        StatusText.Text = _project.Year is { } year
            ? $"Buch enthält nur Einträge aus {year} ({_project.BookEntries.Count()} Einträge)."
            : $"Buch enthält alle Einträge ({_project.Entries.Count}).";
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

    private void EditedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.B) { ToggleMarkup("**"); e.Handled = true; }
        else if (e.Key == Key.I) { ToggleMarkup("*"); e.Handled = true; }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e) => ToggleMarkup("**");
    private void ItalicButton_Click(object sender, RoutedEventArgs e) => ToggleMarkup("*");
    private void ListButton_Click(object sender, RoutedEventArgs e) => ToggleList();

    /// <summary>
    /// Schließt die Markierung in **…** bzw. *…* ein oder entfernt die Auszeichnung wieder.
    /// Mehrzeilige Markierungen werden zeilenweise ausgezeichnet (LatexText.Inline arbeitet pro Zeile).
    /// </summary>
    private void ToggleMarkup(string marker)
    {
        if (CurrentEntry is null) return;
        var box = EditedTextBox;
        var text = box.Text;
        var start = box.SelectionStart;
        var end = start + box.SelectionLength;
        // Leerzeichen am Rand nicht einschließen, sonst erkennt LatexText die Auszeichnung nicht.
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;

        if (start == end)
        {
            box.Select(box.SelectionStart, 0);
            var caret = box.SelectionStart;
            box.SelectedText = marker + marker;
            box.Select(caret + marker.Length, 0);
        }
        else
        {
            // „***x***“ ist fett und kursiv: Sternchen auf beiden Seiten zählen.
            var left = CountStars(text, start - 1, -1);
            var right = CountStars(text, end, 1);
            var isWrapped = marker == "**" ? left >= 2 && right >= 2 : left % 2 == 1 && right % 2 == 1;
            var inner = text[start..end];

            if (isWrapped)
            {
                box.Select(start - marker.Length, inner.Length + 2 * marker.Length);
                box.SelectedText = inner;
                box.Select(start - marker.Length, inner.Length);
            }
            else
            {
                var wrapped = Regex.Replace(inner, @"(?m)^(?<lead>[ \t]*(?:[-•][ \t]+)?)(?<body>[^\r\n]*?)(?<trail>[ \t]*)(?=\r?$)",
                    m => m.Groups["body"].Length == 0 ? m.Value : m.Groups["lead"].Value + marker + m.Groups["body"].Value + marker + m.Groups["trail"].Value);
                box.Select(start, inner.Length);
                box.SelectedText = wrapped;
                if (inner.Contains('\n'))
                    box.Select(start, wrapped.Length);
                else
                    box.Select(start + marker.Length, inner.Length);
            }
        }
        box.Focus();
    }

    private static int CountStars(string text, int index, int step)
    {
        var count = 0;
        for (; index >= 0 && index < text.Length && text[index] == '*'; index += step) count++;
        return count;
    }

    /// <summary>Macht die markierten Zeilen zur Liste („- “) oder hebt die Liste wieder auf.</summary>
    private void ToggleList()
    {
        if (CurrentEntry is null) return;
        var box = EditedTextBox;
        var text = box.Text;
        var start = box.SelectionStart == 0 ? 0 : text.LastIndexOf('\n', box.SelectionStart - 1) + 1;
        var selectionEnd = box.SelectionStart + box.SelectionLength;
        // Endet die Markierung direkt am Zeilenanfang, gehört diese Zeile nicht dazu.
        if (box.SelectionLength > 0 && selectionEnd > start && text[selectionEnd - 1] == '\n') selectionEnd--;
        var end = text.IndexOf('\n', selectionEnd);
        if (end < 0) end = text.Length;

        var lines = text[start..end].Split('\n');
        var listItem = new Regex(@"^[ \t]*[-•][ \t]+");
        var nonEmpty = lines.Where(l => l.Trim().Length > 0).ToList();
        var isList = nonEmpty.Count > 0 && nonEmpty.All(l => listItem.IsMatch(l));
        var changed = string.Join("\n", lines.Select(l =>
            l.Trim().Length == 0 ? l
            : isList ? listItem.Replace(l, string.Empty)
            : listItem.IsMatch(l) ? l : "- " + l.TrimStart()));

        box.Select(start, end - start);
        box.SelectedText = changed;
        box.Select(start, changed.Length);
        box.Focus();
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
