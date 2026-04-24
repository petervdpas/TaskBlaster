using System;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Forms;

public sealed class FormDocumentFactory : IFormDocumentFactory
{
    public IFormDocument CreateDefault() => new FormDocument();

    public IFormDocument LoadFromFile(string path) => FormDocument.LoadFromFile(path);

    public void SaveToFile(IFormDocument document, string path)
    {
        if (document is not FormDocument concrete)
            throw new ArgumentException(
                $"Expected {nameof(FormDocument)}, got {document.GetType().Name}.",
                nameof(document));
        concrete.SaveToFile(path);
    }
}
