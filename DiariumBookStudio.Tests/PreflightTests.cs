using DiariumBookStudio.Models;
using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

public class PreflightTests
{
    private static readonly EntryModel Entry = new() { Title = "Test" };

    [Theory]
    [InlineData(1600, 114, null)]                       // 356 ppi
    [InlineData(800, 114, PreflightSeverity.Hinweis)]   // 178 ppi
    [InlineData(400, 114, PreflightSeverity.Warnung)]   // 89 ppi
    public void Aufloesung_SchwellenFuerHinweisUndWarnung(double pixels, double widthMm, PreflightSeverity? expected)
    {
        var report = new PreflightReport();
        report.CheckResolution(Entry, "IMG.jpg", pixels, widthMm);
        Assert.Equal(expected, report.Issues.SingleOrDefault()?.Severity);
    }

    [Fact]
    public void Zusammenfassung_ZaehltNachArt()
    {
        var report = new PreflightReport();
        Assert.Equal("Keine Auffälligkeiten.", report.Summary);
        report.Add(PreflightSeverity.Hinweis, Entry, "a");
        report.Add(PreflightSeverity.Hinweis, Entry, "b");
        report.Add(PreflightSeverity.Fehler, null, "c");
        Assert.Equal("1 × Fehler, 2 × Hinweis", report.Summary);
    }
}
