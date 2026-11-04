using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiariumBookStudio.Services;

/// <summary>
/// Seitenmaße des Innenteils in mm. Wird vor \usepackage{diarybook} als Makros in book.tex
/// geschrieben (siehe diarybook.sty) und für die Auflösungsprüfung verwendet.
/// </summary>
/// <param name="FontSize">Grundschrift in pt (Klassenoption von scrbook).</param>
public sealed record BookGeometry(
    double TrimWidth, double TrimHeight, double Bleed,
    double Inner, double Outer, double Top, double Bottom,
    double HeadSep, double FootSkip, int FontSize)
{
    public double TextWidth => TrimWidth - Inner - Outer;
    public double TextHeight => TrimHeight - Top - Bottom;

    public string ToLatex()
    {
        var sb = new StringBuilder();
        void Def(string name, double mm) => sb.AppendLine($"\\def\\Diary{name}{{{Mm(mm)}}}");
        Def("TrimWidth", TrimWidth);
        Def("TrimHeight", TrimHeight);
        Def("Bleed", Bleed);
        Def("Inner", Inner);
        Def("Outer", Outer);
        Def("Top", Top);
        Def("Bottom", Bottom);
        Def("HeadSep", HeadSep);
        Def("FootSkip", FootSkip);
        return sb.ToString();
    }

    public static string Mm(double mm) => mm.ToString("0.###", CultureInfo.InvariantCulture) + "mm";
}

/// <summary>
/// Ränder eines Formats in mm. Kopfzeile (Top − HeadSep) und Seitenzahl (Bottom − FootSkip)
/// müssen innerhalb des Sicherheitsabstands der Druckerei liegen.
/// </summary>
/// <param name="MinInner">Innenrand mindestens – wächst bei dicken Büchern mit dem Bundsteg.</param>
public sealed record PageLayout(double MinInner, double Outer, double Top, double Bottom, double HeadSep, double FootSkip, int FontSize);

/// <summary>
/// Vorgaben eines Druckprodukts: Format, Beschnitt, Sicherheitsabstand, Seitenzahlgrenzen,
/// Rückenbreite und Bundsteg. Weitere Druckereien werden als zusätzliche Profile ergänzt.
/// </summary>
public sealed record PrintProfile(
    string Id,
    string Name,
    string Provider,
    // Produktcode für die Lulu Print API (später für die Bestellung)
    string PodPackageId,
    double TrimWidth,
    double TrimHeight,
    double Bleed,
    double SafetyMargin,
    PageLayout Layout,
    int MinPages,
    int MaxPages,
    // Unter dieser Seitenzahl keine Schrift auf dem Buchrücken (Lulu: bis 80 Seiten)
    int MinPagesForSpineText,
    // Rückenbreite in mm in Abhängigkeit von der Seitenzahl
    Func<int, double> SpineWidth,
    // Empfohlener Bundsteg (Innenrand) in mm in Abhängigkeit von der Seitenzahl
    Func<int, double> InnerMargin,
    // Seite für das manuelle Hochladen und die Produktauswahl dort (für die Druckinfo im Paket)
    string OrderUrl,
    IReadOnlyList<string> ProductOptions)
{
    public override string ToString() => Name;

    /// <summary>
    /// Satzspiegel des Buches (Druck- und Leseversion gleich, damit Vorschau und Druck dieselben
    /// Seitenumbrüche haben). Der Innenrand wächst mit der Seitenzahl (Bundsteg).
    /// </summary>
    public BookGeometry InteriorGeometry(int pageCount)
        => new(TrimWidth, TrimHeight, Bleed,
            Inner: Math.Max(Layout.MinInner, InnerMargin(pageCount)),
            Outer: Math.Max(Layout.Outer, SafetyMargin + 2),
            Top: Layout.Top, Bottom: Layout.Bottom, HeadSep: Layout.HeadSep, FootSkip: Layout.FootSkip,
            FontSize: Layout.FontSize);

    /// <summary>Gesamtmaße des Umschlags (Rückseite + Rücken + Vorderseite, mit Beschnitt) in mm.</summary>
    public (double Width, double Height, double Spine) CoverSize(int pageCount)
    {
        var spine = SpineWidth(pageCount);
        return (2 * TrimWidth + spine + 2 * Bleed, TrimHeight + 2 * Bleed, spine);
    }

    /// <summary>Prüft die Seitenzahl gegen die Grenzen des Produkts. null = in Ordnung.</summary>
    public string? CheckPageCount(int pageCount)
    {
        if (pageCount < MinPages) return $"{Name}: mindestens {MinPages} Seiten, das Buch hat {pageCount}.";
        if (pageCount > MaxPages) return $"{Name}: höchstens {MaxPages} Seiten, das Buch hat {pageCount}. Buch aufteilen (z. B. Halbjahresbände) oder weniger Fotos auswählen.";
        return null;
    }
}

public static class PrintProfiles
{
    private const double Inch = 25.4;

    /// <summary>Lulu-Bundsteg nach Seitenzahl (Book Creation Guide, „Gutter Additions“).</summary>
    private static double LuluGutter(int pages) => pages switch
    {
        <= 60 => 0.5 * Inch,
        <= 150 => 0.625 * Inch,
        <= 400 => 1.0 * Inch,
        <= 600 => 1.125 * Inch,
        _ => 1.25 * Inch
    };

    /// <summary>Lulu-Taschenbuch: Rücken = Seiten / 444 + 0,06 in (gilt für alle Formate).</summary>
    private static double LuluPaperbackSpine(int pages) => (pages / 444.0 + 0.06) * Inch;

    private static readonly string[] LuluPaperbackOptions =
    {
        "Bindung: Taschenbuch / Paperback (Perfect Bound)",
        "Innenteil: Premium Color",
        "Papier: 80# White – Coated",
        "Umschlag: Matt (Matte)"
    };

    /// <summary>
    /// Lulu A4-Taschenbuch (Klebebindung), Premium-Farbe auf 80# gestrichenem Papier, matter Umschlag.
    /// Größere Fotos als A5; 12 pt Schrift und breitere Ränder, damit die Zeilen lesbar bleiben.
    /// </summary>
    public static PrintProfile LuluA4Paperback { get; } = new(
        Id: "lulu-a4-pb-premium",
        Name: "Lulu · A4 Taschenbuch · Premium-Farbe",
        Provider: "Lulu",
        PodPackageId: "0827X1169FCPREPB080CW444MXX",
        TrimWidth: 210,
        TrimHeight: 297,
        Bleed: 0.125 * Inch,
        SafetyMargin: 0.5 * Inch,
        Layout: new PageLayout(MinInner: 25, Outer: 22, Top: 27, Bottom: 27, HeadSep: 9, FootSkip: 12, FontSize: 12),
        MinPages: 32,
        MaxPages: 800,
        MinPagesForSpineText: 81,
        SpineWidth: LuluPaperbackSpine,
        InnerMargin: LuluGutter,
        OrderUrl: "https://www.lulu.com/create/print-books",
        ProductOptions: new[] { "Buchformat: A4 (8.27 × 11.69 in / 210 × 297 mm)" }.Concat(LuluPaperbackOptions).ToList());

    /// <summary>
    /// Lulu „Crown Quarto“ 189 × 246 mm (7.44 × 9.68 in) – zwischen A5 und A4. Bei 1600 px breiten
    /// Fotos (Diarium-Export) erreicht ein Foto über die volle Satzbreite noch ca. 280 ppi.
    /// Format laut Lulu Book Creation Guide; Verfügbarkeit als Taschenbuch in Premium-Farbe nicht über die API geprüft.
    /// </summary>
    public static PrintProfile LuluCrownQuartoPaperback { get; } = new(
        Id: "lulu-cq-pb-premium",
        Name: "Lulu · Crown Quarto 189 × 246 mm · Premium-Farbe",
        Provider: "Lulu",
        PodPackageId: "0744X0968FCPREPB080CW444MXX",
        TrimWidth: 189,
        TrimHeight: 246,
        Bleed: 0.125 * Inch,
        SafetyMargin: 0.5 * Inch,
        // Kopfzeile 17 mm, Seitenzahl 14 mm vom Endformat; 12 pt für gut lesbare Zeilen bei ca. 145 mm Satzbreite.
        Layout: new PageLayout(MinInner: 22, Outer: 18, Top: 25, Bottom: 25, HeadSep: 8, FootSkip: 11, FontSize: 12),
        MinPages: 32,
        MaxPages: 800,
        MinPagesForSpineText: 81,
        SpineWidth: LuluPaperbackSpine,
        InnerMargin: LuluGutter,
        OrderUrl: "https://www.lulu.com/create/print-books",
        ProductOptions: new[] { "Buchformat: Crown Quarto (7.44 × 9.68 in / 189 × 246 mm)" }.Concat(LuluPaperbackOptions).ToList());

    /// <summary>
    /// Lulu A5-Taschenbuch (Klebebindung), Premium-Farbe auf 80# gestrichenem Papier, matter Umschlag.
    /// Vorgaben aus dem „Lulu Book Creation Guide“: Beschnitt 0,125 in, Sicherheitsabstand 0,5 in,
    /// 32–800 Seiten, Rücken = Seiten / 444 + 0,06 in, Bundsteg nach Seitenzahl.
    /// </summary>
    public static PrintProfile LuluA5Paperback { get; } = new(
        Id: "lulu-a5-pb-premium",
        Name: "Lulu · A5 Taschenbuch · Premium-Farbe",
        Provider: "Lulu",
        PodPackageId: "0583X0827FCPREPB080CW444MXX",
        TrimWidth: 148,
        TrimHeight: 210,
        Bleed: 0.125 * Inch,
        SafetyMargin: 0.5 * Inch,
        // Kopfzeile: Grundlinie 17 mm vom Endformat, Seitenzahl 14 mm – beide innerhalb von 12,7 mm.
        Layout: new PageLayout(MinInner: 19, Outer: 15, Top: 24, Bottom: 24, HeadSep: 7, FootSkip: 10, FontSize: 11),
        MinPages: 32,
        MaxPages: 800,
        MinPagesForSpineText: 81,
        SpineWidth: LuluPaperbackSpine,
        InnerMargin: LuluGutter,
        OrderUrl: "https://www.lulu.com/create/print-books",
        ProductOptions: new[] { "Buchformat: A5 (5.83 × 8.27 in / 148 × 210 mm)" }.Concat(LuluPaperbackOptions).ToList());

    public static IReadOnlyList<PrintProfile> All { get; } = new[] { LuluA4Paperback, LuluCrownQuartoPaperback, LuluA5Paperback };

    /// <summary>Voreinstellung für neue Projekte (änderbar in den Einstellungen).</summary>
    public static PrintProfile Default => LuluA4Paperback;

    public static PrintProfile Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Default;
}
