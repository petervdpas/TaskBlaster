using System;
using System.IO;
using System.Text;

namespace TaskBlaster.Engine;

/// <summary>
/// TextWriter that emits one full line at a time via a callback.
/// '\r' is ignored; '\n' flushes the current buffer as a line.
/// </summary>
internal sealed class LineBufferingWriter : TextWriter
{
    private readonly Action<string> _onLine;
    private readonly StringBuilder _buf = new();

    public LineBufferingWriter(Action<string> onLine) => _onLine = onLine;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
        {
            _onLine(_buf.ToString());
            _buf.Clear();
        }
        else if (value != '\r')
        {
            _buf.Append(value);
        }
    }

    public override void Write(string? value)
    {
        if (value is null) return;
        foreach (var c in value) Write(c);
    }

    public override void Flush()
    {
        if (_buf.Length == 0) return;
        _onLine(_buf.ToString());
        _buf.Clear();
    }
}
