using System.Text.RegularExpressions;
using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

public class GalleryLayoutTests
{
    private static List<GalleryPhoto> Photos(params double[] aspects)
        => aspects.Select((a, i) => new GalleryPhoto($"f{i}", a, string.Empty, 1600, $"IMG_{i}.jpg")).ToList();

    [Theory]
    [InlineData(1.5)]
    [InlineData(1.5, 1.5)]
    [InlineData(1.33, 0.75, 1.33)]
    [InlineData(1.33, 1.33, 0.75, 1.33, 1.5)]
    [InlineData(0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75)]
    public void JedesFotoWirdGenauEinmalPlatziert(params double[] aspects)
    {
        var photos = Photos(aspects);
        var placed = new List<string>();
        GalleryLayout.Render(photos, (p, fraction) =>
        {
            Assert.InRange(fraction, 0.05, 1.0);
            placed.Add(p.File);
            return "images/" + p.File + ".jpg";
        });
        Assert.Equal(photos.Select(p => p.File).OrderBy(f => f), placed.OrderBy(f => f));
    }

    [Fact]
    public void ZeileFuelltDieSatzbreite()
    {
        // Zwei Querformate in einer Zeile: Breiten + Abstand ergeben knapp die volle Breite.
        var widths = new List<double>();
        GalleryLayout.Render(Photos(1.5, 1.5), (_, fraction) => { widths.Add(fraction); return "x"; });
        Assert.Equal(2, widths.Count);
        Assert.Equal(1.0, widths.Sum() + 0.02, 2);
    }

    [Fact]
    public void EinzelnesHochformatWirdBegrenzt()
    {
        double width = 0;
        GalleryLayout.Render(Photos(0.75), (_, fraction) => { width = fraction; return "x"; });
        // höchstens 0,75 \linewidth hoch → Breite 0,75 × 0,75
        Assert.Equal(0.5625, width, 3);
    }

    [Fact]
    public void Aufloesungsgrenze_VerkleinertDieGanzeReihe()
    {
        // Ein Foto darf höchstens 0,3 \linewidth breit werden: beide Fotos der Reihe werden gleich hoch kleiner.
        var widths = new List<double>();
        GalleryLayout.Render(Photos(1.5, 1.5), (_, fraction) => { widths.Add(fraction); return "x"; },
            maxWidth: p => p.File == "f0" ? 0.3 : 1.0);
        Assert.Equal(0.3, widths[0], 3);
        Assert.Equal(0.3, widths[1], 3);
    }

    [Fact]
    public void Aufloesungsgrenze_EinzelnesFotoWirdKleinerGesetzt()
    {
        double width = 0;
        GalleryLayout.Render(Photos(1.5), (_, fraction) => { width = fraction; return "x"; }, maxWidth: _ => 0.8);
        Assert.Equal(0.8, width, 3);
    }

    [Fact]
    public void Aufloesungsgrenze_GrossesBildFaelltAufReihenZurueck()
    {
        // Drei Fotos: ohne Grenze großes Bild + zwei kleine (Breite links > 0,5).
        var free = new List<double>();
        GalleryLayout.Render(Photos(1.33, 1.33, 1.33), (_, f) => { free.Add(f); return "x"; });
        Assert.True(free[0] > 0.5);

        var limited = new List<double>();
        var tex = GalleryLayout.Render(Photos(1.33, 1.33, 1.33), (_, f) => { limited.Add(f); return "x"; }, maxWidth: _ => 0.5);
        Assert.DoesNotContain("minipage", tex);
        Assert.All(limited, f => Assert.True(f <= 0.5 + 1e-9));
    }

    [Fact]
    public void RueckrufBestimmtDenDateinamen()
    {
        var tex = GalleryLayout.Render(Photos(1.5, 1.5), (p, _) => $"images/{p.File}_klein.jpg");
        Assert.Equal(2, Regex.Matches(tex, @"images/f\d_klein\.jpg").Count);
        Assert.DoesNotContain("{f0}", tex);
    }
}
