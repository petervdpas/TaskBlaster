namespace TaskBlaster.Tests;

/// <summary>
/// Serialises every test that runs a .csx through <see cref="Engine.ScriptBlaster"/>.
/// The runner swaps <c>Console.Out</c> / <c>Console.Error</c> globally during a
/// run, so two tests executing in parallel stomp on each other's captured
/// output. Members of this collection run sequentially.
/// </summary>
[CollectionDefinition("ScriptBlaster", DisableParallelization = true)]
public sealed class ScriptBlasterCollection;
