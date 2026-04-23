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
    Task<BlastResult> RunAsync(
        string scriptText,
        string? scriptPath,
        Action<string> onOutput,
        CancellationToken cancellationToken);
}
