using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TaskBlaster.Views;

/// <summary>
/// Tiny purpose-built Markdown renderer for the chat panel. Produces a
/// real Avalonia visual tree (proper bold/italic, larger headings, fenced
/// code blocks in a bordered surface, bullet lists) — enough fidelity to
/// read AI responses comfortably without dragging in Markdown.Avalonia
/// (which crashes on Linux because it hardcodes Consolas).
///
/// <para>
/// Supports: ATX headings (<c># H1</c>..<c>### H3</c>), bullet lists
/// (<c>- item</c> / <c>* item</c>), numbered lists (<c>1. item</c>),
/// fenced code blocks (<c>```language ... ```</c>), inline <c>**bold**</c>,
/// <c>*italic*</c>, <c>`code`</c>, paragraphs separated by blank lines.
/// Links / tables / blockquotes / images are out of scope — they'd come
/// through as their raw Markdown source, which is still readable.
/// </para>
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>Cross-platform monospace stack — picks Cascadia/Consolas on Windows, DejaVu/Liberation on Linux, Menlo on macOS.</summary>
    public const string MonoFont = "Cascadia Code,Consolas,DejaVu Sans Mono,Liberation Mono,Menlo,Monospace";

    /// <summary>Render a full Markdown string to a vertical stack of blocks.</summary>
    public static Control Render(string markdown)
    {
        var panel = new StackPanel { Spacing = 6 };
        if (string.IsNullOrEmpty(markdown)) return panel;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Fenced code block: capture every line until the closing fence.
            if (TryStartCodeFence(line, out var lang))
            {
                var sb = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // skip closing fence
                panel.Children.Add(BuildCodeBlock(sb.ToString(), lang));
                continue;
            }

            // Skip blank lines — they only matter as paragraph separators
            // inside the prose path below.
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            // Horizontal rule: a line of three or more -, *, or _ (with
            // optional whitespace). Renders as a thin divider.
            if (IsHorizontalRule(line))
            {
                panel.Children.Add(BuildHorizontalRule());
                i++;
                continue;
            }

            // Blockquote: collect consecutive '> ' lines into one block.
            if (line.TrimStart().StartsWith("> ", StringComparison.Ordinal)
                || line.TrimStart() == ">")
            {
                var quoted = new List<string>();
                while (i < lines.Length
                       && (lines[i].TrimStart().StartsWith("> ", StringComparison.Ordinal)
                           || lines[i].TrimStart() == ">"))
                {
                    var t = lines[i].TrimStart();
                    quoted.Add(t.Length <= 2 ? string.Empty : t[2..]);
                    i++;
                }
                panel.Children.Add(BuildBlockquote(string.Join(' ', quoted)));
                continue;
            }

            // Headings (ATX). Anything beyond ### renders as ### so the
            // visual hierarchy stays sane in a narrow chat panel.
            if (TryParseHeading(line, out var headingLevel, out var headingText))
            {
                panel.Children.Add(BuildHeading(headingLevel, headingText));
                i++;
                continue;
            }

            // Bullet / numbered list — gather consecutive items into one block.
            if (IsListItem(line, out _))
            {
                var items = new List<string>();
                while (i < lines.Length && IsListItem(lines[i], out var content))
                {
                    items.Add(content);
                    i++;
                }
                panel.Children.Add(BuildList(items));
                continue;
            }

            // Table: a header row containing '|' followed immediately by a
            // separator row of dashes/colons/pipes. Anything else with a
            // bare '|' falls through to the paragraph path.
            if (LooksLikeTableHeader(lines, i))
            {
                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].Contains('|'))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }
                panel.Children.Add(BuildTable(tableLines));
                continue;
            }

            // Default: a paragraph — one or more non-blank, non-special lines.
            var paraLines = new List<string> { line };
            i++;
            while (i < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[i])
                   && !TryParseHeading(lines[i], out _, out _)
                   && !IsListItem(lines[i], out _)
                   && !TryStartCodeFence(lines[i], out _)
                   && !LooksLikeTableHeader(lines, i)
                   && !IsHorizontalRule(lines[i])
                   && !lines[i].TrimStart().StartsWith("> ", StringComparison.Ordinal))
            {
                paraLines.Add(lines[i]);
                i++;
            }
            panel.Children.Add(BuildParagraph(string.Join(' ', paraLines)));
        }

        return panel;
    }

    // ─────────────────────────── helpers ───────────────────────────

    private static bool TryStartCodeFence(string line, out string language)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            language = trimmed[3..].Trim();
            return true;
        }
        language = string.Empty;
        return false;
    }

    private static bool TryParseHeading(string line, out int level, out string text)
    {
        var trimmed = line.TrimStart();
        var n = 0;
        while (n < trimmed.Length && trimmed[n] == '#') n++;
        if (n is >= 1 and <= 6 && n < trimmed.Length && trimmed[n] == ' ')
        {
            level = n;
            text = trimmed[(n + 1)..].TrimEnd('#', ' ');
            return true;
        }
        level = 0;
        text = string.Empty;
        return false;
    }

    private static bool IsListItem(string line, out string content)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            content = trimmed[2..];
            return true;
        }
        // Numbered list "1. ", "12. ", etc.
        var i = 0;
        while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
        if (i > 0 && i + 1 < trimmed.Length && trimmed[i] == '.' && trimmed[i + 1] == ' ')
        {
            content = trimmed[(i + 2)..];
            return true;
        }
        content = string.Empty;
        return false;
    }

    private static Control BuildHeading(int level, string text)
    {
        // Visual hierarchy capped at three sizes — narrower scale than
        // browser markdown defaults so the panel doesn't shout.
        var size = level switch { 1 => 16d, 2 => 14d, _ => 13d };
        var tb = new SelectableTextBlock
        {
            FontSize = size,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 2),
        };
        ApplyInlines(tb, text);
        return tb;
    }

    private static Control BuildParagraph(string text)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        ApplyInlines(tb, text);
        return tb;
    }

    private static Control BuildList(IReadOnlyList<string> items)
    {
        var stack = new StackPanel { Spacing = 2 };
        foreach (var item in items)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var bullet = new TextBlock
            {
                Text = "• ",
                FontSize = 12,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            var body = new SelectableTextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };
            ApplyInlines(body, item);
            Grid.SetColumn(bullet, 0);
            Grid.SetColumn(body,   1);
            grid.Children.Add(bullet);
            grid.Children.Add(body);
            stack.Children.Add(grid);
        }
        return stack;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        var c = trimmed[0];
        if (c is not ('-' or '*' or '_')) return false;
        var count = 0;
        foreach (var ch in trimmed)
        {
            if (ch == c) count++;
            else if (ch != ' ') return false;
        }
        return count >= 3;
    }

    private static Control BuildHorizontalRule()
    {
        var rect = new Rectangle
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 8),
            Opacity = 0.4,
        };
        if (Application.Current?.TryFindResource("BorderBrush", out var br) == true && br is IBrush brush)
            rect.Fill = brush;
        return rect;
    }

    private static Control BuildBlockquote(string text)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = 12,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
        };
        ApplyInlines(tb, text);
        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 4),
            Margin = new Thickness(0, 2, 0, 2),
            Child = tb,
        };
        if (Application.Current?.TryFindResource("BorderBrush", out var br) == true && br is IBrush brush)
            border.BorderBrush = brush;
        return border;
    }

    private static bool LooksLikeTableHeader(string[] lines, int idx)
    {
        if (idx + 1 >= lines.Length) return false;
        if (!lines[idx].Contains('|')) return false;
        var separator = lines[idx + 1].Trim();
        if (separator.Length == 0) return false;
        // Separator is e.g. "|---|---|" or "| :-- | --: |" — every char
        // must be one of |, -, :, or whitespace, and at least one '-'.
        var hasDash = false;
        foreach (var c in separator)
        {
            if (c == '-') hasDash = true;
            else if (c is not ('|' or ':' or ' ' or '\t')) return false;
        }
        return hasDash;
    }

    private static List<string> SplitTableRow(string row)
    {
        var trimmed = row.Trim();
        // Strip leading / trailing pipe so we don't get empty leading
        // / trailing cells (very common form: "| a | b |").
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|'))   trimmed = trimmed[..^1];
        var cells = new List<string>();
        foreach (var part in trimmed.Split('|'))
            cells.Add(part.Trim());
        return cells;
    }

    private static Control BuildTable(List<string> tableLines)
    {
        // tableLines = [header, separator, ...body rows]
        if (tableLines.Count < 2) return BuildParagraph(string.Join('\n', tableLines));

        var header = SplitTableRow(tableLines[0]);
        var bodyRows = new List<List<string>>();
        for (var r = 2; r < tableLines.Count; r++)
        {
            var row = SplitTableRow(tableLines[r]);
            // Pad short rows so the grid stays rectangular.
            while (row.Count < header.Count) row.Add(string.Empty);
            bodyRows.Add(row);
        }

        var cols = header.Count;
        var grid = new Grid();
        for (var c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < bodyRows.Count + 1; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Header row.
        for (var c = 0; c < cols; c++)
            grid.Children.Add(BuildTableCell(header[c], row: 0, col: c, isHeader: true));
        // Body rows.
        for (var r = 0; r < bodyRows.Count; r++)
        {
            for (var c = 0; c < cols && c < bodyRows[r].Count; c++)
                grid.Children.Add(BuildTableCell(bodyRows[r][c], row: r + 1, col: c, isHeader: false));
        }

        // Wrap in a horizontal scroller — wide tables don't need to push
        // the entire chat panel wider; users can scroll the table itself.
        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = grid,
        };
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 2, 0, 2),
            Child = scroller,
        };
        if (Application.Current?.TryFindResource("BorderBrush", out var br) == true && br is IBrush brush)
            border.BorderBrush = brush;
        return border;
    }

    private static Control BuildTableCell(string text, int row, int col, bool isHeader)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = 12,
            FontWeight = isHeader ? FontWeight.Bold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(8, 4),
        };
        ApplyInlines(tb, text);

        var cell = new Border
        {
            // Right + bottom borders only; left + top come from the
            // adjacent cells. Top-row cells get a top border too.
            BorderThickness = new Thickness(0, isHeader ? 0 : 0, 1, 1),
            Child = tb,
        };
        if (Application.Current?.TryFindResource("BorderBrush", out var br) == true && br is IBrush brush)
            cell.BorderBrush = brush;
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        return cell;
    }

    private static Control BuildCodeBlock(string code, string language)
    {
        var inner = new SelectableTextBlock
        {
            Text = code,
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
        };
        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = inner,
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 2, 0, 2),
            BorderThickness = new Thickness(1),
            Child = scroller,
        };
        // Resolve theme brushes once at render time. A live theme switch
        // won't repaint already-rendered messages, but the next message
        // (or a Clear → re-render) picks up the new theme. Acceptable
        // tradeoff for not having to wire a per-message ResourceReference.
        if (Application.Current?.TryFindResource("BgBrush", out var bgRes) == true && bgRes is IBrush bg)
            border.Background = bg;
        if (Application.Current?.TryFindResource("BorderBrush", out var brRes) == true && brRes is IBrush br)
            border.BorderBrush = br;
        if (!string.IsNullOrEmpty(language))
        {
            // Tiny language label above the block — useful when the model
            // emits multiple fences in a row (csharp / json / bash, etc.).
            var label = new TextBlock
            {
                Text = language,
                FontSize = 10,
                Opacity = 0.6,
                Margin = new Thickness(0, 4, 0, 0),
            };
            return new StackPanel { Children = { label, border } };
        }
        return border;
    }

    /// <summary>
    /// Walk inline markdown (bold / italic / inline code / links /
    /// strikethrough / bare URLs) and append the resulting spans to the
    /// target text block. Anything the parser doesn't recognise falls
    /// through as plain text.
    /// </summary>
    private static void ApplyInlines(SelectableTextBlock tb, string text)
    {
        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();
        var i = 0;
        var sb = new StringBuilder();
        while (i < text.Length)
        {
            var c = text[i];

            // Inline code: `...`
            if (c == '`')
            {
                var close = text.IndexOf('`', i + 1);
                if (close > i)
                {
                    Flush(tb, sb);
                    var code = text[(i + 1)..close];
                    tb.Inlines.Add(new Run(code) { FontFamily = new FontFamily(MonoFont) });
                    i = close + 1;
                    continue;
                }
            }

            // Bold: **...**
            if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (close > i + 1)
                {
                    Flush(tb, sb);
                    var bold = text[(i + 2)..close];
                    tb.Inlines.Add(new Run(bold) { FontWeight = FontWeight.Bold });
                    i = close + 2;
                    continue;
                }
            }

            // Strikethrough: ~~...~~
            if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                var close = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (close > i + 1)
                {
                    Flush(tb, sb);
                    var struck = text[(i + 2)..close];
                    tb.Inlines.Add(new Run(struck) { TextDecorations = TextDecorations.Strikethrough });
                    i = close + 2;
                    continue;
                }
            }

            // Italic: *...*  (only when not part of a ** pair)
            if (c == '*')
            {
                var close = text.IndexOf('*', i + 1);
                if (close > i)
                {
                    Flush(tb, sb);
                    var italic = text[(i + 1)..close];
                    tb.Inlines.Add(new Run(italic) { FontStyle = FontStyle.Italic });
                    i = close + 1;
                    continue;
                }
            }

            // Markdown link: [label](url)
            if (c == '[' && TryParseLink(text, i, out var label, out var url, out var consumed))
            {
                Flush(tb, sb);
                tb.Inlines.Add(BuildLinkRun(label, url));
                i += consumed;
                continue;
            }

            // Bare URL autolinker: http(s)://… up to the next whitespace
            // or sentence-terminating punctuation. Cheap heuristic, good
            // enough for chat — not RFC-compliant.
            if ((c == 'h' || c == 'H') && IsAutoLinkStart(text, i, out var urlEnd))
            {
                Flush(tb, sb);
                var bareUrl = text[i..urlEnd];
                tb.Inlines.Add(BuildLinkRun(bareUrl, bareUrl));
                i = urlEnd;
                continue;
            }

            sb.Append(c);
            i++;
        }
        Flush(tb, sb);
    }

    private static bool TryParseLink(string text, int i, out string label, out string url, out int consumed)
    {
        label = string.Empty; url = string.Empty; consumed = 0;
        // Walk to the closing ']' (allow nested chars but not nested brackets).
        var end = text.IndexOf(']', i + 1);
        if (end <= i) return false;
        if (end + 1 >= text.Length || text[end + 1] != '(') return false;
        var urlEnd = text.IndexOf(')', end + 2);
        if (urlEnd <= end + 1) return false;
        label = text[(i + 1)..end];
        url = text[(end + 2)..urlEnd].Trim();
        consumed = (urlEnd + 1) - i;
        return url.Length > 0;
    }

    private static bool IsAutoLinkStart(string text, int i, out int end)
    {
        end = i;
        if (i + 7 < text.Length
            && (text[i..(i + 7)].Equals("http://", StringComparison.OrdinalIgnoreCase)
                || (i + 8 < text.Length && text[i..(i + 8)].Equals("https://", StringComparison.OrdinalIgnoreCase))))
        {
            // Don't autolink when the URL was already consumed by the
            // markdown-link path above (we'd see "[label](http..." — the
            // '[' branch handles it). Since we got here, it's a bare URL.
            end = i;
            while (end < text.Length && !IsUrlTerminator(text[end])) end++;
            // Strip trailing punctuation that's almost certainly sentence-
            // ending (.,;) so "see http://x.y/path." links to /path not /path.
            while (end > i && (text[end - 1] == '.' || text[end - 1] == ',' || text[end - 1] == ';' || text[end - 1] == ')'))
                end--;
            return end > i + 7;
        }
        return false;
    }

    private static bool IsUrlTerminator(char c) =>
        c is ' ' or '\t' or '\n' or '\r' or '<' or '>' or '"' or '\'' or '`';

    private static Run BuildLinkRun(string label, string url)
    {
        var run = new Run(label)
        {
            TextDecorations = TextDecorations.Underline,
        };
        // Pull the accent colour for links so they read as interactive
        // (theme-aware via TryFindResource at render time — close enough).
        if (Application.Current?.TryFindResource("AccentBrush", out var br) == true && br is IBrush brush)
            run.Foreground = brush;
        // Run is a TextElement, not a Control — can't carry pointer events
        // or a tooltip directly. Visual styling (underline + accent colour)
        // is the cue; the URL itself is part of the selectable text via
        // text selection so it's still copyable.
        _ = url;
        return run;
    }

    private static void Flush(SelectableTextBlock tb, StringBuilder sb)
    {
        if (sb.Length == 0) return;
        tb.Inlines!.Add(new Run(sb.ToString()));
        sb.Clear();
    }
}
