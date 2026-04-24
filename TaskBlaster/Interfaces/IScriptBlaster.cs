using System;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Engine;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Runs a .csx script and streams its captured stdout/stderr to the supplied callback.
/// Implementations own compilation + execution (Roslyn, subprocess, stub, etc.).
/// </summary>
public interface IScriptBlaster
{
    /// <summary>
    /// Run <paramref name="scriptText"/>. When <paramref name="globals"/> is
    /// non-null its public members are exposed as top-level identifiers
    /// inside the script (Roslyn globals). Pass null to run without any
    /// injected bindings — useful for tests and stub callers.
    /// </summary>
    Task<BlastResult> RunAsync(
        string scriptText,
        string? scriptPath,
        Action<string> onOutput,
        ScriptGlobals? globals,
        CancellationToken cancellationToken);
}
