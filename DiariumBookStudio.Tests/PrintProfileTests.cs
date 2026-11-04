using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

/// <summary>Werte aus dem Lulu Book Creation Guide (Taschenbuch, A5).</summary>
public class PrintProfileTests
{
    private static readonly PrintProfile Lulu = PrintProfiles.LuluA5Paperback;

    [Theory]
    [InlineData(32, 3.354)]   // (32 / 444 + 0,06) in
    [InlineData(300, 18.686)]
    [InlineData(800, 47.290)]
    public void Rueckenbreite_NachLuluFormel(int pages, double expectedMm)
        => Assert.Equal(expectedMm, Lulu.SpineWidth(pages), 2);

    [Fact]
    public void Rueckenbreite_EntsprichtMillimeterFormel()
    {
        // Lulu gibt die Formel auch in mm an: Seiten / 17,48 + 1,524 mm
        foreach (var pages in new[] { 50, 150, 400, 700 })
            Assert.Equal(pages / 17.48 + 1.524, Lulu.SpineWidth(pages), 1);
    }

    [Theory]
    [InlineData(40, 19.0)]     // Lulu 12,7 mm, aber nie schmaler als das Standardlayout
    [InlineData(100, 19.0)]    // Lulu 15,9 mm
    [InlineData(151, 25.4)]
    [InlineData(400, 25.4)]
    [InlineData(500, 28.575)]
    [InlineData(700, 31.75)]
    public void Bundsteg_WaechstMitSeitenzahl(int pages, double expectedInner)
        => Assert.Equal(expectedInner, Lulu.InteriorGeometry(pages).Inner, 3);

    [Fact]
    public void Satzspiegel_HaeltSicherheitsabstandEin()
    {
        var g = Lulu.InteriorGeometry(300);
        Assert.True(g.Outer >= Lulu.SafetyMargin);
        // Grundlinie der Kopfzeile und der Seitenzahl liegen innerhalb des Sicherheitsabstands.
        Assert.True(g.Top - g.HeadSep >= Lulu.SafetyMargin + 3);
        Assert.True(g.Bottom - g.FootSkip >= Lulu.SafetyMargin);
        Assert.Equal(3.175, g.Bleed, 3);
    }

    [Fact]
    public void Umschlag_ZweiSeitenPlusRueckenPlusBeschnitt()
    {
        var (width, height, spine) = Lulu.CoverSize(300);
        Assert.Equal(2 * 148 + spine + 2 * 3.175, width, 3);
        Assert.Equal(210 + 2 * 3.175, height, 3);
        Assert.Equal(321.04, width, 2); // im Test am echten Projekt nachgemessen
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(32, true)]
    [InlineData(800, true)]
    [InlineData(801, false)]
    public void Seitenzahl_Grenzen(int pages, bool ok)
        => Assert.Equal(ok, Lulu.CheckPageCount(pages) is null);

    [Fact]
    public void Geometrie_SchreibtMillimeterMitPunkt()
    {
        var latex = Lulu.InteriorGeometry(300).ToLatex();
        Assert.Contains(@"\def\DiaryBleed{3.175mm}", latex);
        Assert.Contains(@"\def\DiaryInner{25.4mm}", latex);
        Assert.DoesNotContain(",", latex);
    }

    [Fact]
    public void A4_GroessereSchriftUndSicherheitsabstand()
    {
        var a4 = PrintProfiles.LuluA4Paperback;
        var g = a4.InteriorGeometry(300);
        Assert.Equal(12, g.FontSize);
        Assert.Equal(210 - 25.4 - 22, g.TextWidth, 3);
        Assert.True(g.Top - g.HeadSep >= a4.SafetyMargin + 3);
        Assert.True(g.Bottom - g.FootSkip >= a4.SafetyMargin);
        Assert.Equal(2 * 210 + a4.SpineWidth(300) + 2 * 3.175, a4.CoverSize(300).Width, 3);
    }

    [Fact]
    public void CrownQuarto_ZwischenA5UndA4()
    {
        var cq = PrintProfiles.LuluCrownQuartoPaperback;
        var g = cq.InteriorGeometry(300);
        Assert.Equal(189 - 25.4 - 18, g.TextWidth, 3);
        Assert.True(g.Top - g.HeadSep >= cq.SafetyMargin + 3);
        Assert.True(g.Bottom - g.FootSkip >= cq.SafetyMargin);
        // 1600 px über die volle Satzbreite: ca. 280 ppi
        Assert.InRange(1600 / (g.TextWidth / 25.4), 270, 290);
        Assert.Contains(cq, PrintProfiles.All);
    }

    [Fact]
    public void Voreinstellung_IstA4()
        => Assert.Same(PrintProfiles.LuluA4Paperback, PrintProfiles.Default);

    [Fact]
    public void Find_UnbekannteIdLiefertVoreinstellung()
        => Assert.Same(PrintProfiles.Default, PrintProfiles.Find("gibt-es-nicht"));
}
