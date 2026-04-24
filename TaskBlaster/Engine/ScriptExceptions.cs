using System;

namespace TaskBlaster.Engine;

/// <summary>
/// Marker for exceptions that represent a known, user-facing condition
/// inside a running script — not a program bug. <see cref="ScriptBlaster"/>
/// renders these as a single line (just the message), skipping the full
/// stack trace. The script still completes with
/// <see cref="BlastStatus.Error"/> so callers can distinguish "ran fine"
/// from "couldn't run".
/// </summary>
public interface IFriendlyScriptException
{
}

/// <summary>
/// Thrown when a script touches the vault but it's still locked —
/// typically because the user cancelled the unlock prompt. Script-level
/// "try again after unlocking" is the expected response.
/// </summary>
public sealed class VaultLockedException : InvalidOperationException, IFriendlyScriptException
{
    public VaultLockedException(string message) : base(message) { }
}
