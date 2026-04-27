using System;
using System.IO;
using System.Linq;
using TaskBlaster.Engine;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests for <see cref="XmlDocReader"/>: well-formed input parses
/// correctly, malformed input degrades gracefully, the no-doc-file case
/// is reported as null (not an exception). Includes a round-trip test
/// against the real Acme.Domain.xml that the SampleModels project ships
/// alongside its DLL.
/// </summary>
public sealed class XmlDocReaderTests : IDisposable
{
    private readonly string _temp;

    public XmlDocReaderTests()
    {
        _temp = ExternalsFixtures.FreshTempFolder("xmldoc");
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void TryRead_ReturnsNullWhenNoXmlBesideDll()
    {
        var dll = Path.Combine(_temp, "NoDocs.dll");
        File.WriteAllBytes(dll, new byte[] { 0x4D, 0x5A }); // pretend MZ header; we never load it

        Assert.Null(XmlDocReader.TryRead(dll));
    }

    [Fact]
    public void TryRead_ReturnsNullForMalformedXml()
    {
        // Bad XML shouldn't kill the caller — they can still get
        // signatures from reflection. Treat as "no docs".
        var dll = Path.Combine(_temp, "Broken.dll");
        var xml = Path.Combine(_temp, "Broken.xml");
        File.WriteAllBytes(dll, new byte[] { 0x4D, 0x5A });
        File.WriteAllText(xml, "this is not xml <");

        Assert.Null(XmlDocReader.TryRead(dll));
    }

    [Fact]
    public void Parse_ExtractsSummaryRemarksParamsAndReturns()
    {
        const string xml = """
            <?xml version="1.0"?>
            <doc>
              <assembly><name>FixturePkg</name></assembly>
              <members>
                <member name="T:FixturePkg.Customer">
                  <summary>
                    A customer in the canonical model.
                    Identifier is opaque.
                  </summary>
                  <remarks>Stable across renames.</remarks>
                </member>
                <member name="M:FixturePkg.OrderService.Place(System.String,System.Int32)">
                  <summary>Places a new order.</summary>
                  <param name="customerId">Who is buying.</param>
                  <param name="quantity">How many units.</param>
                  <returns>The new order's id.</returns>
                </member>
              </members>
            </doc>
            """;

        var set = XmlDocReader.Parse(xml, fallbackAssemblyName: "ignored");

        Assert.Equal("FixturePkg", set.AssemblyName);
        Assert.Equal(2, set.Entries.Count);

        var customer = set.Find("T:FixturePkg.Customer");
        Assert.NotNull(customer);
        // NormalizeText collapses the multi-line summary to a single trimmed line.
        Assert.Equal("A customer in the canonical model. Identifier is opaque.", customer!.Summary);
        Assert.Equal("Stable across renames.", customer.Remarks);
        Assert.Empty(customer.Parameters);

        var place = set.Find("M:FixturePkg.OrderService.Place(System.String,System.Int32)");
        Assert.NotNull(place);
        Assert.Equal("Places a new order.", place!.Summary);
        Assert.Equal("The new order's id.", place.Returns);
        Assert.Equal(2, place.Parameters.Count);
        Assert.Equal("customerId", place.Parameters[0].Name);
        Assert.Equal("Who is buying.", place.Parameters[0].Description);
    }

    [Fact]
    public void Parse_FallsBackToProvidedAssemblyNameWhenMissing()
    {
        // Some xmldoc files (especially older ones) omit the <assembly>
        // element. The caller's hint is the right answer.
        const string xml = """
            <?xml version="1.0"?>
            <doc>
              <members>
                <member name="T:Foo.Bar"><summary>Tiny.</summary></member>
              </members>
            </doc>
            """;

        var set = XmlDocReader.Parse(xml, fallbackAssemblyName: "MyAsm");

        Assert.Equal("MyAsm", set.AssemblyName);
        Assert.Single(set.Entries);
    }

    [Fact]
    public void Parse_HandlesEmptyAndMalformedMembers()
    {
        // Defensive: members without a name attribute should be skipped,
        // not throw. Empty <summary> should normalize to null.
        const string xml = """
            <?xml version="1.0"?>
            <doc>
              <members>
                <member name="T:Good"><summary>Real.</summary></member>
                <member><summary>No id.</summary></member>
                <member name=""><summary>Empty id.</summary></member>
                <member name="T:NullSummary"><summary>   </summary></member>
              </members>
            </doc>
            """;

        var set = XmlDocReader.Parse(xml, fallbackAssemblyName: "x");

        Assert.Equal(2, set.Entries.Count); // only Good + NullSummary survive
        Assert.Null(set.Find("T:NullSummary")!.Summary);
    }

    [Fact]
    public void TryRead_RoundTripsRealAcmeDomainXml()
    {
        // The SampleModels project sets <GenerateDocumentationFile>true and ships
        // Acme.Domain.xml right next to Acme.Domain.dll. Our reader should pick
        // up real-world xmldoc shaped exactly the way the C# compiler emits it.
        // If this test runs before the SampleModels project has been built (e.g.,
        // a fresh clone running tests directly), skip — the wider build target
        // produces it as a side-effect of building TaskBlaster.
        var acmeDomainDll = Path.Combine(
            AppContext.BaseDirectory, "DemoNugets");
        // The .nupkg is staged; the loose DLL we want is in the SampleModels
        // bin folder. Walk up to the repo root and dig.
        var repoRoot = FindRepoRoot();
        if (repoRoot is null) return;
        var dll = Directory.EnumerateFiles(
            Path.Combine(repoRoot, "TaskBlaster.SampleModels", "bin"),
            "Acme.Domain.dll", SearchOption.AllDirectories).FirstOrDefault();
        if (dll is null) return; // SampleModels not built yet — fine, skip.

        var set = XmlDocReader.TryRead(dll);

        Assert.NotNull(set);
        Assert.Equal("Acme.Domain", set!.AssemblyName);
        // We know SampleData.Customers has a one-line summary from when we wrote it.
        var customers = set.Find("P:Acme.Domain.SampleData.Customers");
        Assert.NotNull(customers);
        Assert.Contains("tier", customers!.Summary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Walk up from AppContext.BaseDirectory until we find TaskBlaster.slnx, or null if we hit /.</summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TaskBlaster.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
