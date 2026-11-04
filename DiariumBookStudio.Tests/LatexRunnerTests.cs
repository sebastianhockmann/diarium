using System.IO;
using DiariumBookStudio.Services;

namespace DiariumBookStudio.Tests;

public class LatexRunnerTests
{
    [Theory]
    [InlineData("Output written on book.pdf (281 pages, 2001 bytes).", 281)]
    [InlineData("Output written on\nbook_20261104_120000.pdf (300 pages, 162436957 bytes).", 300)]
    [InlineData("Output written on preview.pdf (1 page, 2001 bytes).", 1)]
    public void PageCount_ausLuaLatexAusgabe(string output, int expected)
        => Assert.Equal(expected, LatexRunner.PageCount(output));

    [Fact]
    public void PageCount_FehltWirftVerstaendlicheMeldung()
        => Assert.Throws<InvalidOperationException>(() => LatexRunner.PageCount("kein PDF"));

    [Fact]
    public void EntryPages_LiestSeitenmarkenAusDemProtokoll()
    {
        var log = Path.GetTempFileName();
        try
        {
            File.WriteAllText(log, "This is LuaHBTeX\nDIARYPAGE:1:2026-01-01-1\nirgendwas\nDIARYPAGE:3:2026-01-02-1\r\nDIARYPAGE:3:2026-01-02-2\n");
            Assert.Equal(new[] { (1, "2026-01-01-1"), (3, "2026-01-02-1"), (3, "2026-01-02-2") }, LatexRunner.EntryPages(log));
        }
        finally
        {
            File.Delete(log);
        }
    }
}
