using System.Linq;
using System.Windows;
using System.Windows.Input;
using DiariumBookStudio.Models;
using DiariumBookStudio.Services;

namespace DiariumBookStudio;

/// <summary>Zeigt das Ergebnis der Vorabprüfung. DialogResult true: PDF trotzdem erzeugen.</summary>
public partial class PreflightWindow : Window
{
    /// <summary>Eintrag, zu dem gesprungen werden soll (Doppelklick oder „Zum Eintrag“).</summary>
    public EntryModel? SelectedEntry { get; private set; }

    /// <param name="allowContinue">false: nur prüfen, ohne Knopf „PDF trotzdem erzeugen“.</param>
    public PreflightWindow(PreflightReport report, bool allowContinue)
    {
        InitializeComponent();
        SummaryText.Text = report.HasIssues ? $"Auffälligkeiten: {report.Summary}" : "Keine Auffälligkeiten – das Buch kann gedruckt werden.";
        IssueList.ItemsSource = report.Issues.OrderBy(i => i.Severity).ThenBy(i => i.Entry?.Date).ToList();
        ContinueButton.Visibility = allowContinue ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Content = allowContinue ? "Abbrechen" : "Schließen";
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void GoToEntry_Click(object sender, RoutedEventArgs e) => GoToSelected();

    private void IssueList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => GoToSelected();

    private void GoToSelected()
    {
        if (IssueList.SelectedItem is not PreflightIssue { Entry: { } entry }) return;
        SelectedEntry = entry;
        DialogResult = false;
    }
}
