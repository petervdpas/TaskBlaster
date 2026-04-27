using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

            // Default: a paragraph — one or more non-blank, non-special lines.
            var paraLines = new List<string> { line };
            i++;
            while (i < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[i])
                   && !TryParseHeading(lines[i], out _, out _)
                   && !IsListItem(lines[i], out _)
                   && !TryStartCodeFence(lines[i], out _))
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
    /// Walk inline markdown (bold / italic / inline code) and append the
    /// resulting <see cref="Run"/> spans to the target text block. Anything
    /// the parser doesn't recognise falls through as plain text.
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

            sb.Append(c);
            i++;
        }
        Flush(tb, sb);
    }

    private static void Flush(SelectableTextBlock tb, StringBuilder sb)
    {
        if (sb.Length == 0) return;
        tb.Inlines!.Add(new Run(sb.ToString()));
        sb.Clear();
    }
}
