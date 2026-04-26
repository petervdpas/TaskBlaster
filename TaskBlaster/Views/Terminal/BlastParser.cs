using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TaskBlaster.Views.Terminal;

/// <summary>
/// Detects the <c>$blast</c>-discriminated single-line JSON messages emitted by
/// <c>UtilBlast.Tabular.Blast</c> and parses them into <see cref="TerminalItem"/>
/// instances. Anything that isn't a recognised Blast line falls back to a
/// <see cref="TextItem"/>.
/// </summary>
internal static class BlastParser
{
    public static TerminalItem ParseOrText(string line)
    {
        if (line is null) return new TextItem(string.Empty);

        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("{\"$blast\"", System.StringComparison.Ordinal) &&
            !trimmed.StartsWith("{ \"$blast\"", System.StringComparison.Ordinal))
        {
            return new TextItem(line);
        }

        try
        {
            var obj = JObject.Parse(trimmed);
            return obj["$blast"]?.ToString() switch
            {
                "heading" => new HeadingItem(
                    obj["text"]?.ToString() ?? string.Empty,
                    obj["level"]?.Value<int>() ?? 1),

                "status" => new StatusItem(
                    obj["text"]?.ToString() ?? string.Empty,
                    ParseLevel(obj["level"]?.ToString())),

                "table" => ParseTable(obj),

                "kv" => ParseKv(obj),

                _ => new TextItem(line),
            };
        }
        catch
        {
            return new TextItem(line);
        }
    }

    private static TerminalStatusLevel ParseLevel(string? raw) => raw switch
    {
        "ok"    => TerminalStatusLevel.Ok,
        "warn"  => TerminalStatusLevel.Warn,
        "error" => TerminalStatusLevel.Error,
        _       => TerminalStatusLevel.Info,
    };

    private static TableItem ParseTable(JObject obj)
    {
        var title = obj["title"]?.ToString();
        var headers = new List<string>();
        if (obj["headers"] is JArray hArr)
            foreach (var h in hArr) headers.Add(h.ToString());

        var rows = new List<IReadOnlyList<string?>>();
        if (obj["rows"] is JArray rArr)
        {
            foreach (var row in rArr)
            {
                var cells = new List<string?>();
                if (row is JArray cArr)
                    foreach (var cell in cArr)
                        cells.Add(cell.Type == JTokenType.Null ? null : cell.ToString());
                rows.Add(cells);
            }
        }

        return new TableItem(title, headers, rows);
    }

    private static KvItem ParseKv(JObject obj)
    {
        var title = obj["title"]?.ToString();
        var pairs = new List<KeyValuePair<string, string?>>();
        if (obj["pairs"] is JObject p)
        {
            foreach (var prop in p.Properties())
            {
                var v = prop.Value;
                pairs.Add(new KeyValuePair<string, string?>(
                    prop.Name,
                    v.Type == JTokenType.Null ? null : v.ToString()));
            }
        }
        return new KvItem(title, pairs);
    }
}
