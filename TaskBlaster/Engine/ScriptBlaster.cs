using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace TaskBlaster.Engine;

/// <summary>
/// In-process .csx runner built on Roslyn scripting.
/// Captures Console.Out/Error and streams each line to the supplied callback.
/// </summary>
public sealed class ScriptBlaster
{
    private static readonly string[] DefaultImports =
    {
        "System",
        "System.IO",
        "System.Linq",
        "System.Text",
        "System.Collections.Generic",
        "System.Threading",
        "System.Threading.Tasks",
    };

    static ScriptBlaster()
    {
        // Force-load Blast assemblies so Roslyn can see them via AppDomain.GetAssemblies().
        _ = typeof(UtilBlast.UtilBlastFactory).Assembly;
        _ = typeof(AzureBlast.MssqlDatabase).Assembly;
        _ = typeof(GuiBlast.Prompts).Assembly;
    }

    public async Task<BlastResult> RunAsync(
        string scriptText,
        string? scriptPath,
        Action<string> onOutput,
        CancellationToken cancellationToken)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var writer = new LineBufferingWriter(onOutput);
        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            var options = ScriptOptions.Default
                .WithImports(DefaultImports)
                .WithReferences(GetLoadableAssemblies())
                .WithFilePath(scriptPath ?? "script.csx")
                .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest);

            await CSharpScript.RunAsync(scriptText, options, cancellationToken: cancellationToken)
                              .ConfigureAwait(false);

            writer.Flush();
            return BlastResult.Ok();
        }
        catch (CompilationErrorException cex)
        {
            writer.Flush();
            foreach (var diag in cex.Diagnostics)
                onOutput(diag.ToString());
            return BlastResult.Error("Compilation failed");
        }
        catch (OperationCanceledException)
        {
            writer.Flush();
            return BlastResult.Cancelled();
        }
        catch (Exception ex)
        {
            writer.Flush();
            onOutput(ex.ToString());
            return BlastResult.Error(ex.Message);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static IEnumerable<Assembly> GetLoadableAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(SafeLocation(a)));
    }

    private static string SafeLocation(Assembly a)
    {
        try { return a.Location; }
        catch { return string.Empty; }
    }
}
