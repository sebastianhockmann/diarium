using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiariumBookStudio.Services;

/// <summary>
/// Wandelt Tagebuchtext in LaTeX um: Sonderzeichen maskieren, typografische
/// Anführungszeichen setzen und die einfache Markdown-Auszeichnung des Importers
/// (**fett**, *kursiv*, „- “ Listen) übersetzen.
/// </summary>
public static class LatexText
{
    /// <summary>Etwa drei Zeilen bei A5 – darunter wird keine Initiale gesetzt.</summary>
    private const int MinInitialParagraphLength = 200;

    /// <summary>Maskiert LaTeX-Sonderzeichen in einem Durchlauf und setzt „deutsche“ Anführungszeichen.</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length + 16);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\textbackslash{}"); break;
                case '&': sb.Append("\\&"); break;
                case '%': sb.Append("\\%"); break;
                case '$': sb.Append("\\$"); break;
                case '#': sb.Append("\\#"); break;
                case '_': sb.Append("\\_"); break;
                case '{': sb.Append("\\{"); break;
                case '}': sb.Append("\\}"); break;
                case '~': sb.Append("\\textasciitilde{}"); break;
                case '^': sb.Append("\\textasciicircum{}"); break;
                case '"': sb.Append(IsOpeningPosition(value, i) ? '„' : '“'); break;
                case '\uFE0F': break; // Emoji-Variantenselektor, ohne Funktion im Druck
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static bool IsOpeningPosition(string text, int index)
    {
        if (index == 0) return true;
        var prev = text[index - 1];
        return char.IsWhiteSpace(prev) || prev is '(' or '[' or '–' or '—' or '/' or '\'';
    }

    /// <summary>Wandelt den Eintragstext in Absätze, Listen und Auszeichnungen um.</summary>
    /// <param name="withInitial">Ersten Buchstaben des Textes als Initiale setzen.</param>
    public static string Body(string? text, bool withInitial)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var blocks = Regex.Split(normalized, "\n\\s*\n");
        var output = new List<string>();
        var first = true;

        foreach (var block in blocks)
        {
            var lines = block.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) continue;

            // Ein Block kann aus normalen Zeilen und Listenzeilen gemischt bestehen.
            var paragraph = new List<string>();
            var items = new List<string>();
            void FlushParagraph()
            {
                if (paragraph.Count == 0) return;
                var latex = string.Join("\\newline\n", paragraph.Select(Inline));
                // Die Initiale ist zwei Zeilen hoch; bei kürzeren Absätzen ragt sie in die Fotos darunter.
                if (first && withInitial && paragraph.Sum(l => l.Length) >= MinInitialParagraphLength)
                    latex = AddInitial(latex);
                first = false;
                output.Add(latex);
                paragraph.Clear();
            }
            void FlushItems()
            {
                if (items.Count == 0) return;
                output.Add("\\begin{itemize}\n" + string.Join("\n", items.Select(i => "\\item " + Inline(i))) + "\n\\end{itemize}");
                first = false;
                items.Clear();
            }

            foreach (var line in lines)
            {
                var item = Regex.Match(line, @"^\s*[-•]\s+(?<text>.+)$");
                if (item.Success)
                {
                    FlushParagraph();
                    items.Add(item.Groups["text"].Value);
                }
                else
                {
                    FlushItems();
                    paragraph.Add(line.Trim());
                }
            }
            FlushParagraph();
            FlushItems();
        }

        return string.Join("\n\n", output);
    }

    /// <summary>Maskiert eine Zeile und übersetzt **fett** und *kursiv*.</summary>
    public static string Inline(string text)
    {
        var escaped = Escape(text);
        escaped = Regex.Replace(escaped, @"\*\*(?=\S)(.+?)(?<=\S)\*\*", "\\textbf{$1}");
        escaped = Regex.Replace(escaped, @"(?<![\*\w])\*(?=\S)(.+?)(?<=\S)\*(?![\*\w])", "\\emph{$1}");
        return escaped;
    }

    private static string AddInitial(string latex)
    {
        var match = Regex.Match(latex, @"^(?<first>\p{Lu}|\p{Ll})(?<rest>\p{L}*)");
        if (!match.Success) return latex;
        var rest = latex[match.Length..];
        return $"\\DiaryInitial{{{match.Groups["first"].Value.ToUpperInvariant()}}}{{{match.Groups["rest"].Value}}}{rest}";
    }
}
