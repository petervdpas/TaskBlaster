using System;

namespace TaskBlaster.Engine;

/// <summary>
/// Root object handed to Roslyn as <c>globals</c> for every .csx run. Its
/// public members are surfaced as top-level identifiers inside the
/// script, so a script can write:
/// <code>
/// var conn = Secrets.Resolve("azure", "prod-sql");
/// </code>
/// Keep this type small and stable — adding a new slot here adds a new
/// top-level identifier to every script in the wild.
/// </summary>
public sealed class ScriptGlobals
{
    public ScriptGlobals(ScriptSecrets secrets)
    {
        Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    /// <summary>Vault accessor — resolves (category, key) pairs to secret values.</summary>
    public ScriptSecrets Secrets { get; }
}
