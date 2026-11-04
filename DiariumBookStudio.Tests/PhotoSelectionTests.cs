using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

public class PhotoSelectionTests
{
    // Fingerabdrücke mit großem Abstand zueinander (verschiedene Motive)
    private static readonly ulong[] Motifs =
    {
        0x0000000000000000, 0xFFFFFFFF00000000, 0x00000000FFFFFFFF, 0xFFFF0000FFFF0000,
        0x0000FFFF0000FFFF, 0xF0F0F0F0F0F0F0F0, 0x0F0F0F0F0F0F0F0F, 0xFF00FF00FF00FF00,
        0x00FF00FF00FF00FF, 0xAAAAAAAAAAAAAAAA
    };

    private static PhotoInfo Photo(int motif, long size = 100_000, int changedBits = 0)
        => new(Motifs[motif] ^ ((1UL << changedBits) - 1), size);

    [Fact]
    public void WenigerFotosAlsErlaubt_AlleAusgewaehlt()
        => Assert.Equal(new[] { 0, 1, 2 }, PhotoSelection.Choose(new[] { Photo(0), Photo(1), Photo(2) }, 6));

    [Fact]
    public void Serienbild_WirdZusammengefasst_DetailreichstesBleibt()
    {
        var photos = new[]
        {
            Photo(0), Photo(1, 90_000), Photo(1, 150_000, changedBits: 3), Photo(1, 120_000, changedBits: 6), Photo(2)
        };
        Assert.Equal(new[] { 0, 2, 4 }, PhotoSelection.Choose(photos, 6));
    }

    [Fact]
    public void DuplikatSpaeterImEintrag_WirdAuchZusammengefasst()
    {
        // Motiv 0, dann 1, dann wieder fast dasselbe Foto wie das erste (das detailreichere bleibt).
        Assert.Equal(new[] { 1, 2 }, PhotoSelection.Choose(new[] { Photo(0, 80_000), Photo(1), Photo(0, 120_000, changedBits: 4) }, 6));
    }

    [Fact]
    public void Aufnahmereihenfolge_NachKameraNummer()
    {
        var images = new[] { "IMG_4554.jpg", "IMG_4503.jpg", "IMG_4542.jpg" }
            .Select((name, i) => new Models.ImageItem { RelativePath = "media/" + name, Order = i + 1 })
            .ToList();
        Assert.Equal(new[] { "IMG_4503.jpg", "IMG_4542.jpg", "IMG_4554.jpg" }, PhotoSelection.CaptureOrder(images).Select(i => i.FileName));
    }

    [Fact]
    public void Aufnahmereihenfolge_OhneKameraNummerBleibtExportReihenfolge()
    {
        var images = new[] { "IMG_4554.jpg", "a1b2c3d4-uuid.jpg", "IMG_4503.jpg" }
            .Select((name, i) => new Models.ImageItem { RelativePath = "media/" + name, Order = i + 1 })
            .ToList();
        Assert.Equal(new[] { "IMG_4554.jpg", "a1b2c3d4-uuid.jpg", "IMG_4503.jpg" }, PhotoSelection.CaptureOrder(images).Select(i => i.FileName));
    }

    [Fact]
    public void ZuVieleFotos_GleichmaessigVerteilt()
    {
        var photos = Enumerable.Range(0, 10).Select(i => Photo(i)).ToList();
        var chosen = PhotoSelection.Choose(photos, 4);
        Assert.Equal(new[] { 0, 3, 6, 9 }, chosen); // erstes und letztes Foto dabei
    }

    [Fact]
    public void PflichtfotoBleibtUndZaehltMit()
    {
        var photos = Enumerable.Range(0, 10).Select(i => Photo(i)).ToList();
        var chosen = PhotoSelection.Choose(photos, 3, required: new[] { 5 });
        Assert.Equal(3, chosen.Count);
        Assert.Contains(5, chosen);
    }

    [Fact]
    public void PflichtfotoInSerie_ErsetztDetailreicheresFoto()
    {
        var photos = new[] { Photo(1, 90_000), Photo(1, 150_000, changedBits: 2) };
        Assert.Equal(new[] { 0 }, PhotoSelection.Choose(photos, 6, required: new[] { 0 }));
    }

    [Fact]
    public void KeineFotosOderMaxNull()
    {
        Assert.Empty(PhotoSelection.Choose(Array.Empty<PhotoInfo>(), 6));
        Assert.Empty(PhotoSelection.Choose(new[] { Photo(0) }, 0));
    }

    [Fact]
    public void Abstand_ZaehltUnterschiedlicheBits()
    {
        Assert.Equal(0, PhotoSelection.Distance(0xFF, 0xFF));
        Assert.Equal(64, PhotoSelection.Distance(0, ulong.MaxValue));
        Assert.Equal(3, PhotoSelection.Distance(0b1010, 0b0101 ^ 0b1000));
    }
}
