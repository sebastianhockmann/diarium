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
public sealed record BookGeometry(
    double TrimWidth, double TrimHeight, double Bleed,
    double Inner, double Outer, double Top, double Bottom,
    double HeadSep, double FootSkip)
{
    public double TextWidth => TrimWidth - Inner - Outer;
    public double TextHeight => TrimHeight - Top - Bottom;

    /// <summary>Bildschirm-/Leseversion ohne Beschnitt – entspricht den Vorgaben in diarybook.sty.</summary>
    public static BookGeometry Screen { get; } = new(148, 210, 0, 19, 15, 18, 20, 6, 10);

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
    int MinPages,
    int MaxPages,
    // Unter dieser Seitenzahl keine Schrift auf dem Buchrücken (Lulu: bis 80 Seiten)
    int MinPagesForSpineText,
    // Rückenbreite in mm in Abhängigkeit von der Seitenzahl
    Func<int, double> SpineWidth,
    // Empfohlener Bundsteg (Innenrand) in mm in Abhängigkeit von der Seitenzahl
    Func<int, double> InnerMargin)
{
    public override string ToString() => Name;

    /// <summary>
    /// Satzspiegel der Druckversion. Kopfzeile und Seitenzahl liegen innerhalb des
    /// Sicherheitsabstands, der Innenrand wächst mit der Seitenzahl (Bundsteg).
    /// </summary>
    public BookGeometry InteriorGeometry(int pageCount)
    {
        // Kopfzeile: Grundlinie Top − HeadSep = 17 mm vom Endformat, Seitenzahl: Bottom − FootSkip = 14 mm.
        var outer = Math.Max(BookGeometry.Screen.Outer, SafetyMargin + 2);
        return new BookGeometry(TrimWidth, TrimHeight, Bleed,
            Inner: Math.Max(BookGeometry.Screen.Inner, InnerMargin(pageCount)),
            Outer: outer, Top: 24, Bottom: 24, HeadSep: 7, FootSkip: 10);
    }

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
        MinPages: 32,
        MaxPages: 800,
        MinPagesForSpineText: 81,
        SpineWidth: pages => (pages / 444.0 + 0.06) * Inch,
        InnerMargin: pages => pages switch
        {
            <= 60 => 0.5 * Inch,
            <= 150 => 0.625 * Inch,
            <= 400 => 1.0 * Inch,
            <= 600 => 1.125 * Inch,
            _ => 1.25 * Inch
        });

    public static IReadOnlyList<PrintProfile> All { get; } = new[] { LuluA5Paperback };

    public static PrintProfile Default => LuluA5Paperback;

    public static PrintProfile Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Default;
}
