using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskBlaster.Knowledge;

/// <summary>
/// Pure function: given a knowledge library and a <see cref="PickerContext"/>,
/// return the subset of blocks that apply, in the order the prompt
/// builder should emit them.
///
/// Algorithm:
/// <list type="number">
///   <item>Find entry points — blocks whose <c>when:</c> rule matches the context.</item>
///   <item>Expand transitively via <c>includes:</c> with cycle detection.</item>
///   <item>Sort: <see cref="KnowledgeBlock.Priority"/> descending (null = least),
///         then <see cref="KnowledgeBlock.Title"/> ascending for stable ordering.</item>
/// </list>
///
/// <para>
/// <b>When-rule grammar (v1):</b> <c>when:</c> is a comma-separated list of
/// rules; ANY rule matching triggers the block. Each rule is one of:
/// <list type="bullet">
///   <item><c>always</c> — always matches.</item>
///   <item><c>tag:foo</c> — matches when <see cref="PickerContext.Tags"/> contains <c>foo</c> (case-insensitive).</item>
///   <item><c>Namespace.Type</c> — matches when an exact loaded type FQN equals the rule.</item>
///   <item><c>Namespace</c> (no dot, or dotted) — matches when a loaded namespace equals the rule, or any loaded FQN starts with the rule plus a dot.</item>
/// </list>
/// Blocks with no <c>when:</c> are never picked as entry points; they only
/// appear in the result via another block's <c>includes:</c>. That gives
/// the user an explicit "off-by-default, only pulled in when referenced"
/// shape for shared/base blocks.
/// </para>
/// </summary>
public static class KnowledgeBlockPicker
{
    /// <summary>Pick the relevant blocks for the given context.</summary>
    public static IReadOnlyList<KnowledgeBlock> Pick(
        IEnumerable<KnowledgeBlock> blocks,
        PickerContext context)
    {
        if (blocks is null) throw new ArgumentNullException(nameof(blocks));
        if (context is null) throw new ArgumentNullException(nameof(context));

        var byId = new Dictionary<string, KnowledgeBlock>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in blocks)
        {
            // Last-write-wins on duplicate ids; the store wouldn't produce
            // duplicates today, but defending against pathological input
            // keeps the picker safe to call from anywhere.
            byId[b.Id] = b;
        }

        var picked = new Dictionary<string, KnowledgeBlock>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in byId.Values)
        {
            if (Matches(entry, context))
                ExpandIncludes(entry, byId, picked);
        }

        return picked.Values
            .OrderByDescending(b => b.Priority ?? int.MinValue)
            .ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ExpandIncludes(
        KnowledgeBlock block,
        IReadOnlyDictionary<string, KnowledgeBlock> byId,
        Dictionary<string, KnowledgeBlock> picked)
    {
        // TryAdd doubles as the visited set: a block already in `picked`
        // is either a real prior pick or a node in the current DFS — either
        // way we stop, which makes cycles safe (A includes B includes A).
        if (!picked.TryAdd(block.Id, block)) return;
        foreach (var includeId in block.Includes)
        {
            if (string.IsNullOrWhiteSpace(includeId)) continue;
            if (byId.TryGetValue(includeId, out var sub))
                ExpandIncludes(sub, byId, picked);
            // Dangling include id — silently dropped for now. A later
            // diagnostic pass could surface these in the audit panel.
        }
    }

    private static bool Matches(KnowledgeBlock block, PickerContext context)
    {
        if (!block.Frontmatter.TryGetValue("when", out var whenRaw)) return false;
        if (string.IsNullOrWhiteSpace(whenRaw)) return false;

        foreach (var ruleRaw in whenRaw.Split(','))
        {
            var rule = ruleRaw.Trim();
            if (rule.Length == 0) continue;
            if (RuleMatches(rule, context)) return true;
        }
        return false;
    }

    private static bool RuleMatches(string rule, PickerContext context)
    {
        if (string.Equals(rule, "always", StringComparison.OrdinalIgnoreCase)) return true;

        if (rule.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tag = rule[4..].Trim();
            if (tag.Length == 0) return false;
            foreach (var t in context.Tags)
                if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Type-name / namespace match. Comparison is case-insensitive even
        // though .NET names aren't — users hand-write these, typos are the
        // common case, and false positives here are cheap (a slightly-wrong
        // block is just unhelpful, not dangerous).
        if (Contains(context.LoadedTypeFqns, rule)) return true;
        if (Contains(context.LoadedNamespaces, rule)) return true;
        foreach (var fqn in context.LoadedTypeFqns)
            if (fqn.StartsWith(rule + ".", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool Contains(IReadOnlySet<string> set, string value)
    {
        foreach (var s in set)
            if (string.Equals(s, value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
