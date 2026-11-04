using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

public class LatexTextTests
{
    [Theory]
    [InlineData(@"a\b", @"a\textbackslash{}b")]
    [InlineData("50% & mehr", @"50\% \& mehr")]
    [InlineData("$#_{}", @"\$\#\_\{\}")]
    [InlineData("~^", @"\textasciitilde{}\textasciicircum{}")]
    public void Escape_MaskiertSonderzeichen(string input, string expected)
        => Assert.Equal(expected, LatexText.Escape(input));

    [Fact]
    public void Escape_SetztDeutscheAnfuehrungszeichen()
        => Assert.Equal("Er sagte „Hallo“ (und „tschüss“)", LatexText.Escape("Er sagte \"Hallo\" (und \"tschüss\")"));

    [Fact]
    public void Escape_EntferntEmojiVariantenselektor()
        => Assert.Equal("☀", LatexText.Escape("☀️"));

    [Theory]
    [InlineData("**fett**", @"\textbf{fett}")]
    [InlineData("*kursiv*", @"\emph{kursiv}")]
    [InlineData("ein **fettes** und *kursives* Wort", @"ein \textbf{fettes} und \emph{kursives} Wort")]
    [InlineData("3 * 4 * 5", "3 * 4 * 5")] // Sternchen mit Leerzeichen sind keine Auszeichnung
    public void Inline_UebersetztAuszeichnung(string input, string expected)
        => Assert.Equal(expected, LatexText.Inline(input));

    [Fact]
    public void Body_TrenntAbsaetzeUndListen()
    {
        var body = LatexText.Body("Erster Absatz\nzweite Zeile\n\n- Punkt eins\n- Punkt zwei", withInitial: false);
        Assert.Equal("Erster Absatz\\newline\nzweite Zeile\n\n\\begin{itemize}\n\\item Punkt eins\n\\item Punkt zwei\n\\end{itemize}", body);
    }

    [Fact]
    public void Body_InitialeNurBeiLangemText()
    {
        Assert.DoesNotContain("DiaryInitial", LatexText.Body("Kurzer Text.", withInitial: true));
        var longText = "Heute " + new string('x', 250);
        Assert.StartsWith(@"\DiaryInitial{H}{eute}", LatexText.Body(longText, withInitial: true));
    }

    [Fact]
    public void Body_LeererTextErgibtNichts()
        => Assert.Equal(string.Empty, LatexText.Body("  \n ", withInitial: true));
}
