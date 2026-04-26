using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

[Collection("ScriptBlaster")]
public class ScriptBlasterTests
{
    private static async Task<(BlastResult result, List<string> output)> RunAsync(string script, CancellationToken ct = default)
    {
        IScriptBlaster blaster = new ScriptBlaster();
        var output = new List<string>();
        var result = await blaster.RunAsync(script, scriptPath: null, output.Add, globals: null, ct);
        return (result, output);
    }

    [Fact]
    public async Task RunAsync_HelloWorld_Ok_AndCapturesOutput()
    {
        var (result, output) = await RunAsync("Console.WriteLine(\"hello\");");

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("hello", output);
    }

    [Fact]
    public async Task RunAsync_MultipleWrites_CapturesAllLines()
    {
        var (result, output) = await RunAsync("""
            Console.WriteLine("one");
            Console.WriteLine("two");
            Console.WriteLine("three");
            """);

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Equal(new[] { "one", "two", "three" }, output);
    }

    [Fact]
    public async Task RunAsync_WriteWithoutNewline_Flushed()
    {
        // Console.Write (no newline) — the LineBufferingWriter.Flush at finish should emit it.
        var (result, output) = await RunAsync("Console.Write(\"partial\");");

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("partial", output);
    }

    [Fact]
    public async Task RunAsync_CompilationError_ReturnsError()
    {
        var (result, _) = await RunAsync("this is not C# code @@@@");

        Assert.Equal(BlastStatus.Error, result.Status);
    }

    [Fact]
    public async Task RunAsync_CompilationError_LogsDiagnostics()
    {
        var (_, output) = await RunAsync("Console.WriteLine(unknownVariable);");

        // The diagnostic should mention the undefined variable somewhere.
        Assert.Contains(output, line => line.Contains("unknownVariable") || line.Contains("CS0103"));
    }

    [Fact]
    public async Task RunAsync_RuntimeException_ReturnsError()
    {
        var (result, output) = await RunAsync("throw new System.InvalidOperationException(\"boom\");");

        Assert.Equal(BlastStatus.Error, result.Status);
        // Friendly summary is the exception message; full stack rides along
        // in Details so the terminal can show it as a collapsible section.
        Assert.Contains("boom", result.Message ?? string.Empty);
        Assert.Contains("InvalidOperationException", result.Details ?? string.Empty);
        Assert.Empty(output);
    }

    [Fact]
    public async Task RunAsync_PreCancelledToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var (result, _) = await RunAsync("Console.WriteLine(\"should not run\");", cts.Token);

        Assert.Equal(BlastStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RunAsync_CanReferenceBlastNugets_UtilBlast()
    {
        // ScriptBlaster force-loads the Blast assemblies; scripts should see them.
        var (result, output) = await RunAsync("Console.WriteLine(typeof(UtilBlast.UtilBlastFactory).Name);");

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("UtilBlastFactory", output);
    }

    [Fact]
    public async Task RunAsync_CanReferenceBlastNugets_GuiBlast()
    {
        var (result, output) = await RunAsync("Console.WriteLine(typeof(GuiBlast.Prompts).Name);");

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("Prompts", output);
    }

    [Fact]
    public async Task RunAsync_CanReferenceBlastNugets_AzureBlast()
    {
        var (result, output) = await RunAsync("Console.WriteLine(typeof(AzureBlast.MssqlDatabase).Name);");

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("MssqlDatabase", output);
    }
}
