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
    public void RueckrufBestimmtDenDateinamen()
    {
        var tex = GalleryLayout.Render(Photos(1.5, 1.5), (p, _) => $"images/{p.File}_klein.jpg");
        Assert.Equal(2, Regex.Matches(tex, @"images/f\d_klein\.jpg").Count);
        Assert.DoesNotContain("{f0}", tex);
    }
}
