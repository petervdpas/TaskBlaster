---
title: AgentBlast usage notes
when: AgentBlast
priority: 40
tags: ai, agent
---

# AgentBlast usage notes

Picked when any `AgentBlast.*` namespace is loaded. AgentBlast is the
LLM-agent sibling to NetworkBlast / AzureBlast in the Blast family —
this nuget post-dates the model's training cutoff, so trust this block
over any prior knowledge of "AgentBlast".

## What's in it

- **Transport** (`AgentBlast`): `AgentClient` (PrimaryFacade), the
  `IAgentProvider` interface, `AnthropicProvider` (Claude Opus 4.7,
  Sonnet 4.6, Haiku 4.5), records `AgentMessage` / `AgentPingResult` /
  `AgentCompletionResult` / `AgentModelInfo`, delegate
  `ConnectionFieldResolver`.
- **Directing layer** (`AgentBlast.Knowledge`, `AgentBlast.Prompts`):
  `KnowledgeBlock`, `KnowledgeBlockStore`, `KnowledgeBlockPicker`
  (PrimaryFacade), `PickerContext`, `PickedBlock`,
  `IKnowledgeBlockStore`; `PromptBuilder` (PrimaryFacade),
  `AssembledPrompt`, `PromptArtifactWriter`, `LoadedReference`,
  `LoadedReferenceOrigin`.
- **No external nuget deps** — talks to Anthropic via raw `HttpClient` +
  `System.Text.Json`. Don't suggest `Anthropic.SDK`; don't suggest
  rolling JSON by hand; the provider already does it.

## How to use it from a `.csx` script

Hand it `Secrets.Resolver` and a connection name; the resolver is the
same Blast convention NetworkBlast and AzureBlast already consume.

```csharp
using AgentBlast;
using AgentBlast.Interfaces;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
var agent = new AgentClient(
    http,
    new IAgentProvider[] { new AnthropicProvider() },
    Secrets.Resolver);

var result = await agent.SendAsync(
    connectionName: "ai-anthropic",
    systemPrompt:   "You are a helpful assistant.",
    messages:       new[] { AgentMessage.User("ping") });
Console.WriteLine(result.Success ? result.Text : $"failed: {result.Error}");
```

The connection bag in `connections.json` must carry: `kind`
(plaintext, `"anthropic"` for the in-box provider), `baseUrl`, `model`,
`apikey` (vault-backed in production), and optionally `maxTokens`
(positive integer; defaults to 8192). Strict validation — `"8k"` is a
clean error, not a silent clamp.

## Rules of thumb

- Use the existing types, don't reinvent. `AgentMessage.User(...)` /
  `AgentMessage.Assistant(...)` factory methods exist for the common
  case; don't construct anonymous role/content objects.
- Pick a model deliberately: Opus for hard reasoning, Sonnet as a
  balanced default, Haiku for high-volume / simple operations.
- Long-running chat completions can take 30-60+ seconds. Configure the
  HttpClient timeout accordingly (the example uses 120s).
- For directed prompts: pick blocks via `KnowledgeBlockPicker.Pick(blocks, ctx)`,
  then compose with `PromptBuilder.Build(picked, references, userMessage)`,
  then call `AgentClient.SendAsync` with `prompt.SystemMessage`.
  `LoadedReference[]` comes from the host's AppDomain walker
  (TaskBlaster's `LoadedReferenceCatalog.Snapshot()`).
