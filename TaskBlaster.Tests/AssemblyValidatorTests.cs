using System;
using System.IO;
using System.Linq;
using TaskBlaster.Externals;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests for <see cref="AssemblyValidator"/>: clean path, identity-name
/// version conflicts, and the no-issue baseline. Focused on what the
/// validator can reliably detect via static metadata inspection — the
/// runtime smoke test (<c>GetTypes</c> after <c>LoadFrom</c>) lives in
/// the manager and is exercised there.
/// </summary>
public sealed class AssemblyValidatorTests : IDisposable
{
    private readonly string _temp;

    public AssemblyValidatorTests()
    {
        _temp = ExternalsFixtures.FreshTempFolder("validator");
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void Inspect_StandaloneAssembly_ReturnsNoIssues()
    {
        var dll = Path.Combine(_temp, "Clean.dll");
        ExternalsFixtures.BuildDll(dll, "Clean", new Version(1, 0, 0, 0));

        var report = AssemblyValidator.Inspect(dll, Array.Empty<string>());

        Assert.Equal("Clean",   report.AssemblyName);
        Assert.Equal("1.0.0.0", report.AssemblyVersion);
        Assert.False(report.HasErrors);
        Assert.False(report.HasWarnings);
    }

    [Fact]
    public void Inspect_ConflictingExternalAtDifferentVersion_RaisesError()
    {
        // Already-imported v1; candidate is v2 with the same simple name.
        var existing = Path.Combine(_temp, "Twin.dll");
        ExternalsFixtures.BuildDll(existing, "Twin", new Version(1, 0, 0, 0));

        var candidateFolder = Path.Combine(_temp, "candidate");
        Directory.CreateDirectory(candidateFolder);
        var candidate = Path.Combine(candidateFolder, "Twin.dll");
        ExternalsFixtures.BuildDll(candidate, "Twin", new Version(2, 0, 0, 0));

        var report = AssemblyValidator.Inspect(candidate, new[] { existing });

        Assert.True(report.HasErrors);
        var error = report.Issues.Single(i => i.Level == IssueLevel.Error);
        Assert.Contains("Twin",     error.Message);
        Assert.Contains("conflict", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_SameNameSameVersion_DoesNotConflict()
    {
        // Re-importing the same package version is benign — the dialog
        // shouldn't shout error.
        var existing = Path.Combine(_temp, "Same.dll");
        ExternalsFixtures.BuildDll(existing, "Same", new Version(1, 0, 0, 0));

        var candidateFolder = Path.Combine(_temp, "candidate");
        Directory.CreateDirectory(candidateFolder);
        var candidate = Path.Combine(candidateFolder, "Same.dll");
        ExternalsFixtures.BuildDll(candidate, "Same", new Version(1, 0, 0, 0));

        var report = AssemblyValidator.Inspect(candidate, new[] { existing });

        Assert.False(report.HasErrors);
    }

    [Fact]
    public void Inspect_BadPath_ReturnsErrorWithoutThrowing()
    {
        // Unreadable input should land as an error inside the report rather
        // than bubble up as an exception — the dialog can render that.
        var report = AssemblyValidator.Inspect(
            Path.Combine(_temp, "does-not-exist.dll"),
            Array.Empty<string>());

        Assert.True(report.HasErrors);
        Assert.Contains(report.Issues, i =>
            i.Level == IssueLevel.Error &&
            i.Message.Contains("Cannot read", StringComparison.OrdinalIgnoreCase));
    }
}
