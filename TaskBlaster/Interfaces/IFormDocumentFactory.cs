namespace TaskBlaster.Interfaces;

/// <summary>
/// Builds <see cref="IFormDocument"/> instances. One document per loaded
/// form, so this is effectively a transient factory — keeping the
/// construction behind an interface lets tests and consumers resolve it
/// from the DI container without calling <c>FormDocument</c> statics directly.
/// </summary>
public interface IFormDocumentFactory
{
    /// <summary>Create an empty document seeded with the default form.</summary>
    IFormDocument CreateDefault();

    /// <summary>Load a form JSON file from disk and wrap it in a document.</summary>
    IFormDocument LoadFromFile(string path);

    /// <summary>Save the given document to the supplied path.</summary>
    void SaveToFile(IFormDocument document, string path);
}
