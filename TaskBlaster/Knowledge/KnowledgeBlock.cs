using System.Collections.Generic;

namespace TaskBlaster.Knowledge;

/// <summary>
/// A single piece of directing-context the user has authored for the AI
/// assistant. Backed by one markdown file under the knowledge folder.
/// The file basename (without extension) is the stable id; the title
/// shown in the UI comes from the frontmatter when present and falls
/// back to a humanised id otherwise.
/// </summary>
/// <param name="Id">File basename without the .md extension; stable identity used by the store.</param>
/// <param name="Title">Display title from frontmatter, or a humanised id when frontmatter omits it.</param>
/// <param name="Body">Markdown body — the file content with frontmatter stripped.</param>
/// <param name="Frontmatter">Raw key/value pairs from the frontmatter block; empty when the file has none.</param>
public sealed record KnowledgeBlock(
    string Id,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Frontmatter);
