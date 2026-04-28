using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Engine;

/// <summary>
/// In-process .csx runner built on Roslyn scripting.
/// Captures Console.Out/Error and streams each line to the supplied callback.
/// </summary>
public sealed class ScriptBlaster : IScriptBlaster
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

    private static int _warmedUp;

    /// <summary>
    /// Force-load the Blast-family assemblies so Roslyn can see them via
    /// <see cref="AppDomain.GetAssemblies"/> (and so
    /// <c>LoadedReferenceCatalog</c> includes them in its snapshot). Idempotent;
    /// safe to call from multiple sites. Was previously a static constructor
    /// running at app startup; now deferred to the first call site that
    /// actually needs the assemblies — first script <see cref="RunAsync"/> or
    /// the host's first scripting-ready gate. Saves cold-start cost on slow
    /// systems for users who only browse / edit / manage secrets without
    /// running a script.
    /// </summary>
    public static void WarmupBlasts()
    {
        if (Interlocked.CompareExchange(ref _warmedUp, 1, 0) != 0) return;
        _ = typeof(UtilBlast.UtilBlastFactory).Assembly;
        _ = typeof(AzureBlast.MssqlDatabase).Assembly;
        _ = typeof(GuiBlast.Prompts).Assembly;
        _ = typeof(NetworkBlast.NetClient).Assembly;
        _ = typeof(SqliteBlast.SqliteStore).Assembly;
    }

    public async Task<BlastResult> RunAsync(
        string scriptText,
        string? scriptPath,
        Action<string> onOutput,
        ScriptGlobals? globals,
        CancellationToken cancellationToken)
    {
        WarmupBlasts();
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

            // Off-load to a thread-pool thread so the UI dispatcher stays free.
            // Blocking calls (e.g. GuiBlast Prompts, Secrets.Resolve) need the UI
            // thread to be pumping.
            await Task.Run(async () =>
            {
                if (globals is null)
                {
                    await CSharpScript.RunAsync(scriptText, options,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await CSharpScript.RunAsync(scriptText, options,
                        globals: globals,
                        globalsType: typeof(ScriptGlobals),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is IFriendlyScriptException)
        {
            writer.Flush();
            // Known user-facing condition (e.g. user cancelled the vault
            // unlock prompt). Treat as a graceful abort, not a crash:
            // no stack trace, Cancelled status so the terminal renders
            // ⊘ instead of ✗.
            return BlastResult.Cancelled(ex.Message);
        }
        catch (Exception ex)
        {
            writer.Flush();
            // Friendly one-liner for the terminal header; full ToString() rides
            // along as the expandable details so genuine bugs still have full
            // diagnostics one click away.
            var summary = FormatExpectedException(ex) ?? ex.Message;
            return BlastResult.Error(summary, details: ex.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    /// <summary>
    /// Classifies common operational failures (network unreachable, timeout,
    /// IO/permission errors) and returns a one-line summary instead of a
    /// full stack trace. Returns <c>null</c> for everything else, leaving
    /// the generic catch block to dump <see cref="Exception.ToString"/> so
    /// genuine library bugs still surface with full diagnostics.
    /// </summary>
    private static string? FormatExpectedException(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            switch (cur)
            {
                case HttpRequestException:
                case SocketException:
                    return $"Network: {cur.Message}";
                case TimeoutException:
                    return $"Timeout: {cur.Message}";
                case UnauthorizedAccessException:
                    return $"Access denied: {cur.Message}";
                case FileNotFoundException:
                case DirectoryNotFoundException:
                    return $"Not found: {cur.Message}";
                case IOException:
                    return $"IO: {cur.Message}";
            }
        }
        return null;
    }

    private static IEnumerable<Assembly> GetLoadableAssemblies()
    {
        // Roslyn reads assemblies from disk when we hand them to
        // WithReferences, so an assembly whose backing file has been
        // deleted (uninstalled external, moved package, broken symlink)
        // would explode the entire compilation. Filter those out so
        // every script compile stays robust to AppDomain cruft.
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a =>
            {
                var loc = SafeLocation(a);
                return !string.IsNullOrEmpty(loc) && File.Exists(loc);
            });
    }

    private static string SafeLocation(Assembly a)
    {
        try { return a.Location; }
        catch { return string.Empty; }
    }
}
