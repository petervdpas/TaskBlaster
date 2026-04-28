using System;
using UtilBlast.Interfaces;

namespace TaskBlaster.Engine;

/// <summary>
/// Root object handed to Roslyn as <c>globals</c> for every .csx run. Its
/// public members are surfaced as top-level identifiers inside the
/// script, so a script can write:
/// <code>
/// var conn = Secrets.Resolve("azure", "prod-sql");
/// var json = await Folders.ReadTextAsync("forms", "deploy");
/// </code>
/// Keep this type small and stable — adding a new slot here adds a new
/// top-level identifier to every script in the wild.
/// </summary>
public sealed class ScriptGlobals
{
    public ScriptGlobals(ScriptSecrets secrets, IBlastContext ctx)
    {
        Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        Ctx     = ctx     ?? throw new ArgumentNullException(nameof(ctx));
        Folders = ctx.Folders;
    }

    /// <summary>Vault accessor — resolves (category, key) pairs to secret values.</summary>
    public ScriptSecrets Secrets { get; }

    /// <summary>
    /// Ambient UtilBlast context. Future harness slots (loggers,
    /// resolvers, …) plug in here; today only <see cref="IBlastContext.Folders"/>
    /// is populated by the harness, and that's already surfaced as
    /// <see cref="Folders"/> for shorter call sites.
    /// </summary>
    public IBlastContext Ctx { get; }

    /// <summary>
    /// Named-folder registry — read/write files by logical name without
    /// touching <c>Path.Combine</c>. The TaskBlaster harness registers
    /// <c>"forms"</c>, <c>"scripts"</c>, and <c>"vault"</c> at startup.
    /// </summary>
    public IBlastFolders Folders { get; }
}
