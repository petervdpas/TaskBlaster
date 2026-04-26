using System.Collections.Generic;

namespace TaskBlaster.Views.Terminal;

/// <summary>
/// Base type for everything that can stream into the terminal panel. The terminal's
/// <c>ItemsControl</c> selects a widget per item type, so plain text and structured
/// "Blast" messages render side-by-side in the same scrolling pane (LINQPad style).
/// </summary>
public abstract record TerminalItem;

/// <summary>A plain text line — what every legacy <c>Console.WriteLine</c> still produces.</summary>
public sealed record TextItem(string Line) : TerminalItem;

/// <summary>Heading produced by <c>UtilBlast.Tabular.Blast.Heading(text, level)</c>.</summary>
public sealed record HeadingItem(string Text, int Level) : TerminalItem;

/// <summary>Status produced by <c>UtilBlast.Tabular.Blast.Status(text, level)</c>.</summary>
public sealed record StatusItem(string Text, TerminalStatusLevel Level) : TerminalItem;

/// <summary>Table produced by <c>UtilBlast.Tabular.Blast.Table(result, title?)</c>.</summary>
public sealed record TableItem(
    string? Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> Rows) : TerminalItem;

/// <summary>Key/value snapshot produced by <c>UtilBlast.Tabular.Blast.Kv(obj, title?)</c>.</summary>
public sealed record KvItem(
    string? Title,
    IReadOnlyList<KeyValuePair<string, string?>> Pairs) : TerminalItem;

/// <summary>Status severity displayed alongside <see cref="StatusItem"/>.</summary>
public enum TerminalStatusLevel
{
    Info,
    Ok,
    Warn,
    Error,
}
