using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using DiariumBookStudio.Models;
using DiariumBookStudio.Services;
using Microsoft.Win32;

namespace DiariumBookStudio;

public partial class SettingsWindow : Window
{
    /// <summary>Die geänderten Einstellungen (nur gültig, wenn der Dialog mit „Speichern“ geschlossen wurde).</summary>
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings.Clone();
        ProjectsFolderBox.Text = Settings.ProjectsFolder;
        PackageFolderBox.Text = Settings.PrintPackageFolder;
        LuaLatexBox.Text = Settings.LuaLatexPath;
        ProfileBox.ItemsSource = PrintProfiles.All;
        ProfileBox.SelectedItem = PrintProfiles.Find(Settings.DefaultPrintProfileId);
        MaxPhotosBox.Text = Settings.DefaultMaxPhotosPerEntry.ToString();
        OpenLastProjectCheckBox.IsChecked = Settings.OpenLastProject;
    }

    private void BrowseProjectsFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(ProjectsFolderBox, "Projektordner wählen");

    private void BrowsePackageFolder_Click(object sender, RoutedEventArgs e) => BrowseFolder(PackageFolderBox, "Ordner für Druckpakete wählen");

    private void BrowseFolder(System.Windows.Controls.TextBox box, string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(box.Text)) dialog.InitialDirectory = box.Text;
        if (dialog.ShowDialog(this) == true) box.Text = dialog.FolderName;
    }

    private void BrowseLuaLatex_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "lualatex.exe auswählen",
            Filter = "lualatex.exe|lualatex.exe|Programme (*.exe)|*.exe",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        if (dialog.ShowDialog(this) == true) LuaLatexBox.Text = dialog.FileName;
    }

    private async void TestLuaLatex_Click(object sender, RoutedEventArgs e)
    {
        var command = string.IsNullOrWhiteSpace(LuaLatexBox.Text) ? "lualatex" : LuaLatexBox.Text.Trim();
        LuaLatexInfo.Text = "Prüfe...";
        try
        {
            var psi = new ProcessStartInfo(command, "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            LuaLatexInfo.Text = $"Gefunden: {firstLine}";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LuaLatexInfo.Text = "Nicht gefunden. MiKTeX installieren (miktex.org) oder den Pfad zu lualatex.exe angeben.";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var projects = ProjectsFolderBox.Text.Trim();
        if (projects.Length == 0) projects = AppSettings.DefaultProjectsFolder;
        try
        {
            Directory.CreateDirectory(projects);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(this, $"Der Projektordner kann nicht angelegt werden:\n{ex.Message}", "Einstellungen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var lualatex = LuaLatexBox.Text.Trim();
        if (lualatex.Length > 0 && !File.Exists(lualatex))
        {
            MessageBox.Show(this, "Die angegebene lualatex.exe gibt es nicht.", "Einstellungen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.ProjectsFolder = projects;
        Settings.PrintPackageFolder = PackageFolderBox.Text.Trim();
        Settings.LuaLatexPath = lualatex;
        Settings.DefaultPrintProfileId = (ProfileBox.SelectedItem as PrintProfile ?? PrintProfiles.Default).Id;
        Settings.DefaultMaxPhotosPerEntry = int.TryParse(MaxPhotosBox.Text, out var max) && max > 0 ? max : 6;
        Settings.OpenLastProject = OpenLastProjectCheckBox.IsChecked == true;
        DialogResult = true;
    }
}
