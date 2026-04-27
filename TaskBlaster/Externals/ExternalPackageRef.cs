namespace TaskBlaster.Externals;

/// <summary>
/// Identity of a NuGet package that TaskBlaster has imported into its
/// own package store under <c>~/.taskblaster/packages/&lt;Id&gt;/&lt;Version&gt;/</c>.
/// The actual DLLs live on disk; this record is what gets persisted to
/// <c>config.json</c> so we can reload them on next launch.
/// </summary>
public sealed record ExternalPackageRef(string Id, string Version);
