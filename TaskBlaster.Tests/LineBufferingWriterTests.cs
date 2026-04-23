using System.Collections.Generic;
using TaskBlaster.Engine;

namespace TaskBlaster.Tests;

public class LineBufferingWriterTests
{
    [Fact]
    public void Write_PartialLine_DoesNotEmit()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("hello");

        Assert.Empty(lines);
    }

    [Fact]
    public void WriteLine_EmitsSingleLine()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("hello\n");

        Assert.Single(lines);
        Assert.Equal("hello", lines[0]);
    }

    [Fact]
    public void Write_MultipleLinesInOneBuffer_EmitsEach()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("one\ntwo\nthree\n");

        Assert.Equal(new[] { "one", "two", "three" }, lines);
    }

    [Fact]
    public void Write_CarriageReturn_IsIgnored()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("hello\r\nworld\r\n");

        Assert.Equal(new[] { "hello", "world" }, lines);
    }

    [Fact]
    public void Write_ConsecutiveNewlines_EmitsEmptyLines()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("a\n\nb\n");

        Assert.Equal(new[] { "a", "", "b" }, lines);
    }

    [Fact]
    public void Write_Null_NoEffect()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write((string?)null);
        w.Flush();

        Assert.Empty(lines);
    }

    [Fact]
    public void Flush_WithBufferedPartial_EmitsIt()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("incomplete");
        w.Flush();

        Assert.Single(lines);
        Assert.Equal("incomplete", lines[0]);
    }

    [Fact]
    public void Flush_WhenEmpty_EmitsNothing()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Flush();
        w.Flush();

        Assert.Empty(lines);
    }

    [Fact]
    public void Write_ThenFlush_AfterFullLine_NoExtraEmit()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        w.Write("hello\n");
        w.Flush();

        Assert.Single(lines);
    }

    [Fact]
    public void Write_OneCharAtATime_StillProducesLines()
    {
        var lines = new List<string>();
        var w = new LineBufferingWriter(lines.Add);

        foreach (var c in "abc\n12\n") w.Write(c);

        Assert.Equal(new[] { "abc", "12" }, lines);
    }
}
