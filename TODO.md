# TODO

## Next — post-Secrets-tab follow-ups

The Secrets tab is live (SecretBlast 1.0.0 wired in). Scripts can now
pull values out of the vault via the `Secrets` global (see the Done
section below). Still open:

1. **Connections: legacy import wizard.** Phase 3 of the multipart
   connections work (Phase 1 model+resolver, Phase 2 Connections tab
   landed 2026-04-26). Wizard would read a flat / fully-plaintext
   legacy connections JSON, let the user mark which fields per
   connection should move into the vault, write those vault entries,
   and rewrite the connections file with `fromVault` pointers in
   their place. Only worth building if there's a real ScriptRunner.Plugins
   data set to migrate from.
2. **Connections UI: vault picker.** The Connections tab currently
   uses two free-text TextBoxes for the (category, key) pair on
   FromVault fields. Replace with a pair of ComboBoxes populated from
   `IVaultService.GetCategoriesAsync` + a keys-by-category lookup,
   reusing the OptionsPropertyEditor pattern (vault unlock on demand).
3. **Connections UI: reveal toggle for plaintext fields.** Long
   plaintext values (especially anything that looks secret-shaped)
   could use a 👁 toggle similar to `SecretEntryDialog`. Optional, low
   priority.
4. **Value-column reveal (open question).** The Secrets grid currently shows
   Category / Key / Description / Updated only; the value lives behind 📋 Copy
   and the 👁 toggle in `SecretEntryDialog`. If we ever add a value column,
   gate it behind per-row reveal or a "reveal for 30 s" pattern. Otherwise
   close this item.
5. **Per-script (project-style) external references.** Today's External tab is
   global: every imported nupkg/dll loads into the AppDomain on first
   scripting-ready trigger (first script Run / first chat send / first
   Assistant Preview — see the lazy-load sweep in the Done log) and is
   visible to every script after that. That model has two real pain points:

   - Two scripts that need different versions of the same package can't
     coexist (the default `AssemblyLoadContext` refuses two same-simple-name
     assemblies; current upgrade flow stages for next-launch only).
   - A script's dependencies aren't self-describing; a fresh user opening
     a `.csx` has no idea which externals it needs.

   A "project" model would scope references per-script (or per-folder):

   - In-script directive: `#r "package: Acme.Domain, 1.0.0"` resolved from
     `~/.taskblaster/packages/`, missing entries surface a one-click
     "Import this package" prompt.
   - Or sidecar metadata: `<script>.deps.json` listing the references,
     editable from a new pane in the editor toolbar.
   - Engine: each script run gets its own `AssemblyLoadContext` so two
     scripts can host different versions side-by-side and old versions
     get GC'd when the run completes.

   Worth doing once the global model starts hurting (multi-version conflicts
   in real usage, or scripts being shared between teams that need explicit
   manifests). Until then the simpler global model is fine for the
   "everyone uses one canonical-models package" case.
6. **Formidable → C# round-trip companion script.** The forward leg
   (`acme-domain-to-formidable.csx`, landed 2026-04-29) reflects a loaded
   DLL into FCDM forms via `AssemblyBlast.AssemblyReader`. The return leg
   would `GET /api/collections/fcdm-{entities,enums}?include=data`,
   re-hydrate the items into `ClassDefinition[]` / `EnumDefinition[]`,
   and run `AssemblyBlast.AssemblyWriter.WriteToFolder(...)` to write
   `.cs` files into a configurable target folder (namespace-as-path
   layout). Closes the design ↔ development loop: the FCDM form is the
   design surface, the `.cs` files are derived. Drift detection
   (`git status` against the target folder) is the natural next step
   after that.

## Roadmap (in-app)

### Directed AI (script assistant)

Working name: **Directed AI**. The pattern is: the user actively
*directs* the AI's behavior via explicit, visible knowledge — and can
see exactly which knowledge fired on any given suggestion. "Directed"
carries both halves: you can't direct what you can't see, so the
visibility is implicit in the verb.

> **Status snapshot (2026-04-28):** Foundation shipped — see the Done log
> for the AgentBlast 1.0.0 extraction + Phase B consumption. Live today:
> `AgentClient` + Anthropic provider, knowledge-block store + Assistant
> tab, picker + prompt builder, audit trail under `~/.taskblaster/ai-history/`,
> per-script chat panel, "(none — Agent disabled)" sentinel that hides
> the 🧠 Assistant button + 💬 Chat toggle when no provider is configured,
> connection-driven config (`kind` / `baseUrl` / `model` / `apikey` /
> `maxTokens`). Still open: the named operations below, OpenAI / Ollama
> providers, first-run BYOK wizard, per-session cost guardrails, the MCP
> server stage.

The differentiator vs Copilot / Cursor / general LLM chat is **state
Copilot can't see**: TaskBlaster knows what's in the vault (categories
+ keys, never values), what's in `connections.json`, which externals
are loaded (`Acme.Domain.Customer` etc.), what forms exist nearby, and
what Blast helpers are available. A generic LLM doesn't and will
happily suggest hardcoded credentials or reinvent helpers we already
have.

Operating principles:

- **Manual trigger only.** Button in the script editor toolbar; never
  auto-suggest, never auto-apply. Output is a diff the user reviews.
- **Never auto-execute.** Suggestions modify text only; running is still a
  deliberate user action via the Run button.
- **Bring your own key, with local model as a peer.** API key persisted
  in the vault (very on-brand, reuses the existing encrypted store).
  Provider dropdown lists OpenAI, Anthropic, **and Ollama as first-class
  peers** — not "cloud + power-user fallback". This audience often runs
  in regulated environments where the corp-approved key (or
  no-cloud-at-all) is the only viable option, so local must feel
  intentional, not vestigial. No TaskBlaster-hosted proxy: we stay out
  of the data path.
- **Friction-softeners for BYOK.** Feature shows a clear "no key
  configured → disabled" state instead of a broken button. First-run
  wizard walks through getting a key (links + screenshots per provider).
  Cost guardrails surfaced in Settings: max tokens per request, max
  requests per session, optional "warn before each call" confirmation.
- **Send structure, not secrets.** Vault context = category + key names
  (already non-sensitive, surface organisation). Connections context =
  field names + plaintext values (server / baseUrl) but never resolved
  vault refs. Externals context = exported type signatures.

Discipline note: "optimize this" produces forgettable results. Ship a
small set of *named operations* and resist the open-ended chat surface.
Each named op has a clear success criterion (does the diff actually do
the thing) and is easier to evaluate, easier to ship, easier to mark as
broken when it isn't working.

First operations worth building:

1. **Convert inline prompts → form file.** Detect repeated `Prompts.Input`
   / `Prompts.Confirm` calls; offer to write a JSON form to
   `~/.taskblaster/forms/<name>.json` and replace the prompts with
   `DynamicForm.ShowJsonAsync` loading that file.
2. **Use a connection instead of inline credentials.** Detect
   connection-string fragments / hardcoded URLs that match an existing
   `connections.json` entry; offer to replace with
   `Secrets.GetConnection<T>` or `Secrets.Resolver`.
3. **Use loaded external types instead of dictionaries.** Detect
   `Dictionary<string, object>` / anonymous-type usage whose shape
   matches a record in a loaded external (e.g. `Acme.Domain.Customer`);
   offer the typed rewrite.
4. **Replace ad-hoc output with the Blast display DSL.** Detect
   `Console.WriteLine` separator strings (`===`, `---`), kv-style
   `$"  {k,-20} = {v}"` lines; offer the equivalent `Blast.WriteHeading`
   / `Blast.WriteKv` / `Blast.WriteTable` rewrite.

Supporting work in the Blast nugets (cross-cutting): **shipped**
(2026-04-27). Each Blast nuget's main assembly now stamps an
`[AssemblyMetadata("Blast.PrimaryFacade", "FQN1,FQN2,...")]` attribute
naming its 1-4 canonical entry points; consumed by
`LoadedReferenceCatalog` (also shipped). See the Done log for details.

### Knowledge blocks — the directing layer

A user-curated library of small text blocks that get injected into AI
context. **This is what makes the AI directed**: the blocks are how
the user steers, and they're how the user audits.

The reframing that makes this important: **it's not just "make the AI
smarter" — it's "make the AI auditable"**. With explicit, user-visible
blocks of influence:

- The AI's output shows which blocks fired.
- The user can read the blocks → understand *why* the suggestion
  came out the way it did.
- The user can edit blocks → change future behavior deterministically.
- The user can disable blocks → debug "is this block making things
  worse?".

This is the user reclaiming agency over the influence layer. Different
product category from "AI assistant" — closer to "AI you can reason
about". Solves a problem hosted LLMs structurally can't:
domain knowledge that lives in heads / Confluence / tribal knowledge
and is invisible to a generic model.

File layout (proposed):

```
~/.taskblaster/knowledge/
├── api-conventions.md       # camelCase JSON, snake_case SQL
├── customer-domain.md       # Customer.Code is stable, not Customer.Id
├── azure-sql-patterns.md    # Code samples for our warehouse
├── queue-drain-runbook.md   # Operational steps
└── form-templates.md        # Standard approval form shape
```

Each block is a markdown file with frontmatter:

```yaml
---
title: Customer domain conventions
priority: high
when: always           # or: when-using=Acme.Domain
                       # or: when-operation=convert-to-form
                       # or: when-loaded=NetworkBlast
---
# body — free-form markdown the LLM reads as context
```

Composition rules (from the "it's combinatorics" conversation):

- **Knowledge × operations × loaded-externals are orthogonal.** Free
  composition is fine; that's where the leverage comes from. A "SQL
  conventions" block + Acme.Domain types + the "convert to typed query"
  operation produces a rewrite none of those alone could.
- **Provider stays global.** Per-operation provider config is chaos —
  one dropdown in Settings, applies everywhere.
- **Scope is hierarchical.** Script-local (sidecar `.knowledge/` folder
  next to the script) overrides global, doesn't interleave.
- **Scope tags are the combinatoric pruner.** A block tagged
  `when-using=Acme.Domain` only fires when the script actually loads
  Acme.Domain types. Lets a user maintain 50 blocks without the AI
  seeing 50 blocks every call.

Three injection models, ranked by complexity:

1. **Always-on (v1).** Every AI call gets every block whose `when:`
  rule matches. Cheapest to build, predictable, fine for ~few KB of
  total knowledge. Likely the right starting point.
2. **Auto-relevant retrieval (v2).** AI sees titles + descriptions of
  available blocks, asks for the ones it thinks are relevant. Costs an
  extra round-trip. Needed once knowledge libraries grow past a few
  dozen blocks.
3. **User-pinned (always available alongside the above).** User
  explicitly tags which blocks apply to which scripts. Boring but
  transparent — the escape hatch when auto-rules misbehave.

Auditability surface (this is what makes the feature distinctive):

- Every AI call emits a "blocks used: [...]" trace shown to the user.
- A "blocks influencing this suggestion" panel in the validation /
  review UI lists each fired block with a one-click "open" / "disable
  for this call" / "edit" action.
- Optional per-call audit log (local-only, kept under
  `~/.taskblaster/ai-history/`) captures the exact prompt + block set
  + response. Lets the user retrospect "why did it suggest X yesterday?"

Team sharing (this is what makes the feature a product):

- Knowledge folder is plain text + git-friendly — a team's
  `~/.taskblaster/knowledge/` checked into a repo IS their TaskBlaster
  domain expertise. New hire clones the repo, points TaskBlaster at it,
  inherits the team's accumulated knowledge.
- This is also TaskBlaster's answer to the "no story for sharing
  scripts" critique: the sharing surface isn't the scripts themselves,
  it's the knowledge that informs how scripts get written.

Lineage to be aware of:

- `CLAUDE.md` (Claude Code) — single always-on file, no scope rules.
- `.cursorrules` (Cursor) — same shape, different tool.
- Anthropic MCP — server-based context plumbing; heavier than what we
  need but worth understanding the protocol.
- OpenAI Custom GPTs / Assistants file_search — RAG over uploaded
  files, hosted-only.

None of these are local-first + git-friendly + vault-aware in the way
TaskBlaster could be. Steal the good ideas (markdown + frontmatter,
scope-based activation, audit trail), skip the SaaS shape.

Open questions before any code:

- How do scope rules combine when multiple blocks match? Just include
  all of them up to a token budget? Or rank by `priority:` and
  truncate?
- Sidecar `.knowledge/` folder per script — or annotate the script
  header with `// @knowledge: customer-domain, sql-patterns` to
  declare per-script overrides?
- What's the v1 frontmatter schema? Start narrow (`title`, `priority`,
  `when`) and expand only when concrete needs surface — same
  scope-discipline as the AI operations themselves.

Open questions for the AI assistant in general:

- Streaming vs single-shot response (streaming = nicer UX, but harder
  to show as a reviewable diff).
- Should the assistant see *other* scripts in the folder for "use the
  pattern this user already uses" suggestions, or stay strictly to the
  active file?
- Per-script enable/disable (some scripts shouldn't ever be sent to a
  remote LLM — flag in a comment header? sidecar file?).
- Telemetry: do we log which suggestions were accepted / rejected so
  we can tune the prompts? Local-only if so.

### TaskBlaster as an MCP server (future stage)

Once Directed AI is shipped (consumer side: TaskBlaster calls Claude /
GPT / Ollama via API key in vault), the natural extension is the
**other direction**: TaskBlaster becomes an MCP (Model Context
Protocol) server that Claude Desktop / Claude Code can connect to.

What it'd expose to a connecting client:

- **Loaded references** — the same `LoadedReferenceCatalog` snapshot
  we built for the consumer side. Claude in another window asks "what
  Acme.Domain types exist" and gets the answer from TaskBlaster's
  AppDomain.
- **Vault structure** — categories + key names (never values), same
  as the consumer side already permits.
- **Connections inventory** — names + field names, no resolved values.
- **Knowledge blocks** — once the directing layer exists, TaskBlaster's
  knowledge folder becomes available to any MCP client. A team's
  `~/.taskblaster/knowledge/` checked into git suddenly informs not
  just TaskBlaster but every Claude session anyone runs.
- **Optional tools** — "run script X with these inputs", "preview
  this form spec", with explicit per-tool consent.

Why it's worth the future investment: the same primitives we're
building for in-app Directed AI become a multiplier when other AI
tools can read them too. The user gets value from TaskBlaster even
when they're not writing scripts inside it.

Sparked by user observation 2026-04-27 after seeing Anthropic's
Connect surface in their dashboard. Not for the v1 cycle; revisit
once the consumer-side Directed AI ships and stabilises.

## Roadmap (separate repos)

*(empty; all currently-planned siblings have shipped: NetworkBlast 1.0.2,
AzureBlast 2.1.0, GuiBlast 2.1.0, SecretBlast 1.0.2.)*

## Done

### 2026-04-29 — AssemblyBlast round-trip (1.1 → 1.2) + FCDM/Formidable demo

AssemblyBlast grew the second half of its loop and TaskBlaster gained a
working **DLL → Formidable FCDM → DLL** demo against a locally-running
[Formidable](https://github.com/petervdpas/Formidable) instance.

**AssemblyBlast 1.1.0 — `AssemblyReader`.** Reflects any loaded
`Assembly` into the same `ClassDefinition[]` / `EnumDefinition[]` shapes
the generator path consumes. Captures namespace + kind (class / record
/ struct / interface), base type, implemented interfaces, every public
constructor's parameters, public properties with full nullability and
collection unwrapping, ctor-fed property detection (so `public string
Id { get; }` set in a public ctor is *not* flagged as derived), and
XML-doc summaries when the sibling `<Name>.xml` ships next to the dll.
New `EnumDefinition` + `EnumMemberDefinition` model types added (members
with values normalised to `long`, `IsFlags` set from the `[Flags]`
attribute).

**AssemblyBlast 1.1.1 — auto-implemented interface filter + static-class
skip.** Records' compiler-generated `IEquatable<TSelf>` is filtered from
`Implements` (kept on hand-written non-record `IEquatable<T>` cases —
distinction enforced by tests). `static` classes (CLR-side: sealed +
abstract) are skipped by `ReadClasses` since they're not domain types.

**AssemblyBlast 1.1.2 — nullable ctor parameter preservation.** Reader
now keeps NRT `string? line2` and value-type `int? count` annotated as
`string?` / `int?` in `ParameterDefinition.Type`, instead of stripping
them. Both `NullableAttribute` (param-level) and the surrounding
`NullableContextAttribute` (method / type fallback) are honoured.

**AssemblyBlast 1.2.0 — `AssemblyWriter`.** Mirror of the reader: turns
a `ClassDefinition` or `EnumDefinition` back into C# source with
file-scoped namespace, XML-doc preservation, ctor body assignments
derived from a case-insensitive ctor-param ↔ property match
(`line1 → Line1 = line1;`), accessor-aware property rendering
(`{ get; }` / `{ get; init; }` / `{ get; set; }`), nullability +
`List<T>` collection wrapping, `[Flags]` and underlying-type emission
for enums (omitted for `int` default). `WriteToFolder` lays out files
under a namespace-as-path tree (`Acme.Domain.Crm` →
`Acme/Domain/Crm/`). 6 new tests, all green.

**SampleModels (`Acme.Domain`) refactored to the canonical Fontys
domain shape.** Was records with positional ctors; is now a `class` per
file with `public Foo(...)` ctor that assigns `Property = paramName;`
into auto-properties with `{ get; init; }`. Truly computed members
preserved get-only (`Person.FullName`, `Order.Total`,
`OrderLine.LineTotal`).

**Demo script `acme-domain-to-formidable.csx`.** Generic
"reflect any loaded External assembly into Formidable" tool — picks the
target by name (`const string AssemblyName = "Acme.Domain";`), no
static type reference, no `using Acme.Domain;`. Walks
`AppDomain.CurrentDomain.GetAssemblies()` to find the named DLL,
runs `AssemblyReader.ReadClasses` / `ReadEnums`, maps each definition
to an FCDM entity / enum payload, and POSTs both batches to
`collections/fcdm-{entities,enums}/batch?mode=replace` via
`NetworkBlast.NetClient` over the named `Formidable` connection. GUIDs
are MD5-derived from `Namespace.Name`, so reruns are idempotent.

**Companion changes in Formidable** (separate repo, written but unpublished
by us — published by the user):

- `POST /api/collections/{template}` and `PUT .../{id}` now accept
  `?upsert=true` for idempotent reimports.
- New `POST /api/collections/{template}/batch?mode=create|replace|merge`
  endpoint: bulk apply many items in one request, per-item failures
  collected in `errors` rather than aborting the batch.
- New entries get a slug-from-`item_field` filename (e.g.
  `customer.meta.json`) instead of `<guid>.meta.json`, with numeric
  suffix on collision.
- `fcdm-entities.yaml` template: new `constructor-parameters` table
  (Type, Naam columns) + new `accessor` column on the `attributes`
  table so the markdown_template can render the C# class shape with the
  ctor body fully expanded (assignments + property accessors). New
  `cell` / `pascal` / `camel` Handlebars helpers in the markdown
  renderer.

End result: opening any FCDM-Entities form in Formidable shows the
class rendered as proper C# in the HTML preview pane, complete with
ctor signature, ctor body assignments, property accessors with the
right shape per case, and XML doc summaries. The reverse half (a
Formidable → `.cs` script using `AssemblyWriter` to regenerate the
project) is the next planned step in the design ↔ development loop.

### 2026-04-28 — AgentBlast 1.0.0 extraction + agent UI rename + lazy-load sweep

Big consolidation day. The Directed-AI work that had been growing inside
TaskBlaster moved to its own nuget, the user-facing terminology shifted
from "AI" to "Agent", and the cold-start path got a thorough lazy-load pass.

**AgentBlast 1.0.0 — new sibling Blast nuget at `~/Projects/AgentBlast/`,
published to nuget.org.**

- Carved out `TaskBlaster/Ai/` (`AiClient`, `AnthropicProvider`,
  `AssembledPrompt`, `PromptBuilder`, `PromptArtifactWriter`),
  `TaskBlaster/Knowledge/*` (block model + store + picker + picker
  context + picked-block reason), `TaskBlaster/Interfaces/IKnowledgeBlockStore.cs`,
  and the `LoadedReference` record + `LoadedReferenceOrigin` enum
  extracted from `Engine/LoadedReferenceCatalog.cs`. Catalog (the
  AppDomain walker) stays in TaskBlaster as the producer; AgentBlast
  defines the shape.
- Renames: `AiClient → AgentClient`, `IAiProvider → IAgentProvider`,
  `AiMessage → AgentMessage`, `AiPingResult → AgentPingResult`,
  `AiCompletionResult → AgentCompletionResult`, `AiModelInfo → AgentModelInfo`.
  `ConnectionFieldResolver` delegate name kept (it's the cross-Blast
  resolver shape, not AI-specific).
- Pre-extraction provider decoupling: `IAiProvider` was already
  refactored to take a `ConnectionFieldResolver` delegate instead of
  `Connection` + `IVaultService`, so the lift to AgentBlast carried
  zero TaskBlaster types. DI in TaskBlaster wires the resolver from
  `ConnectionsResolver` over a locked-state-tolerant `vault.ResolveAsync`
  (returns empty when locked rather than throwing — UI does the unlock
  pre-flight).
- AgentBlast layout: `AgentClient.cs` (records + delegate + dispatcher),
  `AnthropicProvider.cs`, `Interfaces/IAgentProvider.cs`,
  `Interfaces/IKnowledgeBlockStore.cs`, `Knowledge/{KnowledgeBlock,
  KnowledgeBlockPicker, KnowledgeBlockStore, PickedBlock, PickerContext}.cs`,
  `Prompts/{PromptBuilder, AssembledPrompt, PromptArtifactWriter,
  LoadedReference}.cs`. `Blast.PrimaryFacade` attribute lists three
  entries: `AgentBlast.AgentClient`, `AgentBlast.Knowledge.KnowledgeBlockPicker`,
  `AgentBlast.Prompts.PromptBuilder`.
- Tests: 105 in AgentBlast.Tests (24 AgentClient, 30 picker, 30 store,
  14 prompt-builder, 7 artifact-writer). Strict Release build with
  `TreatWarningsAsErrors=true` is clean — every public member documented.
- TaskBlaster Phase B: dropped the local copies, added
  `<PackageReference Include="AgentBlast" Version="1.0.0" />`,
  retargeted `Program.cs` / `MainWindow` / `Dialogs/ConfigDialog` /
  `Views/ScriptChatView` / `Views/AssistantView` / `Engine/LoadedReferenceCatalog`
  to the AgentBlast types. 244 TaskBlaster tests + 105 AgentBlast tests
  = 349 green across both repos.
- New demo knowledge block: `DemoKnowledge/agentblast-when-loaded.md`.
  Fires when any `AgentBlast.*` namespace is in scope. Tells the model
  what AgentBlast is (it post-dates the training cutoff), the
  connection schema, the host-glue pattern via `Secrets.Resolver`, and
  the rules of thumb for the API surface.

**Agent UI rename — "AI" → "Agent" in user-visible strings.**

- Settings tab `Header="AI"` → `"Agent"`; "AI provider connection" →
  "Agent provider connection"; "Directed-AI provider" → "Agent provider";
  "Other AI specifics" → "Other Agent specifics"; sentinel
  `"(none — AI disabled)"` → `"(none — Agent disabled)"`.
- ScriptChatView errors: "No AI provider configured. Open Settings → AI"
  → "No Agent provider configured. Open Settings → Agent"; "AI provider
  'X' not found" → "Agent provider 'X' not found".
- Internal `AiDefaultProvider` C# property + JSON key kept for
  back-compat with existing `~/.taskblaster/config.json` files.

**Toolbar visibility gating.**

- `IsAiEnabled()` helper checks both `AiDefaultProvider` is set AND that
  the named connection still exists. New `ApplyAiAvailability()`:
  hides the 🧠 Assistant mode button + 💬 Chat toggle when no provider
  is configured, and bumps the user back to Scripts mode if they were
  sitting on the Assistant tab when the provider got cleared. Runs on
  startup and after every Settings save where the AI provider changed.
- New `IsAssistantModeVisible` property on `ToolbarView` mirrors
  `IsChatToggleVisible`. Scripts-mode chat toggle is now also gated on
  `IsAiEnabled()`.

**Lazy-load sweep.**

- `Lazy<AgentClient>` and `Lazy<IKnowledgeBlockStore>` registered in DI
  alongside their unwrapped counterparts. `MainWindow` / `ScriptChatView`
  / `AssistantView` / `ConfigDialog` field types and ctor / Initialize
  parameters changed accordingly. `.Value` accessed only on actual
  user-triggered work (chat send, Settings → Agent Test, Assistant tab
  Reload, AssistantView preview). On Agent-disabled launches none of
  the AgentBlast machinery (HttpClient, AnthropicProvider, resolver
  delegate) is instantiated; the KnowledgeBlockStore folder scan
  doesn't run.
- `ScriptBlaster`'s static constructor moved to `WarmupBlasts()` (a
  public static method, Interlocked-guarded). Was force-loading
  UtilBlast / AzureBlast / GuiBlast / NetworkBlast / SqliteBlast at
  type-load time (= startup, since DI resolves `IScriptBlaster` for
  MainWindow.ctor). Now deferred to first call site that actually
  needs them — first script `RunAsync`, or the host's
  `EnsureScriptingReady()` gate.
- `ExternalReferenceManager.LoadAll()` moved out of MainWindow.ctor
  body into a one-shot `EnsureScriptingReady()` method. Called from
  `OnRunClicked`, and via an `Action ensureScriptingReady` callback
  plumbed into `ScriptChatView.Initialize` (`UpdateContextHint`,
  `BuildPickerContext`) and `AssistantView.Initialize` (`OnPreviewClicked`).
  External-load terminal logs now appear at the moment the user first
  triggers scripting work, not at app launch.
- `AssistantView.InstallMarkdownHighlighter` moved out of the ctor.
  TextMate registry construction (loads TextMateSharp.Grammars — multi-MB
  of grammar JSON) now happens on first `Reload()` (= first time the
  user enters the Assistant tab), via an idempotent
  `EnsureMarkdownHighlighter()` guard. The eager `Reload()` call in
  `Initialize` was dropped; SwitchMode(Assistant) is the trigger.
- Net cold-start path on Agent-off / default-highlighter / no-Assistant-visit
  launches: Avalonia bootstrap, ConfigStore.Load (tiny JSON),
  ConnectionStore.Reload (tiny JSON), SeedMissingFromFolder stat() loop,
  view XAML parse + event wiring, ApplyTerminalVisibility,
  SwitchMode(Scripts). Everything else is on-demand.

**Docs.**

- README: Stack section bumped to current Blast versions and added
  `AgentBlast 1.0.0`. "How it works" got a step 6 for the agent. The
  "AI assistant (Directed AI)" heading became "Agent assistant
  (Directed AI)" with assistant→agent prose updates throughout, plus
  a mention that the tab + chat toggle are hidden until a provider is
  configured. The bundled-demos table grew the
  `agentblast-when-loaded.md` row.

### 2026-04-27 (cont. 5) — Knowledge blocks: store + Assistant tab + typed model

First user-visible Directed-AI surface. The Assistant tab lets the
user create / edit / delete knowledge blocks (markdown files under
`~/.taskblaster/knowledge/`) that will become AI directing context
once the picker + prompt builder land.

Model + store:

- **`Knowledge/KnowledgeBlock`** record: `Id` (file basename),
  `Title` (from frontmatter or humanised id), `Body` (markdown), `Priority`
  (`int?`), `Tags` (`IReadOnlyList<string>`), `Includes`
  (`IReadOnlyList<string>`), plus the raw `Frontmatter` dict for any
  hand-added keys we don't model yet.
- **`Knowledge/KnowledgeBlockStore`** + **`Interfaces/IKnowledgeBlockStore`**.
  Reads `*.md` from the configured folder, parses YAML-style frontmatter
  fenced by two `---` lines, and surfaces typed properties for the
  well-known keys. `ParseList` (public, used by the UI on save too)
  trims, lowercases, deduplicates comma-separated tokens. Serialize
  writes well-known keys first in fixed order (title → when → priority
  → tags → includes → other) so hand-edited files stay predictable.
  Empty lists drop the key entirely so we never emit `tags:` with no
  value. Title only round-trips when it differs from the auto-humanised
  id (no auto-generated title lines polluting clean files).
- **DI**: `IKnowledgeBlockStore` registered as a singleton anchored on
  `Path.GetDirectoryName(VaultFolder)` + `"knowledge"`, matching the
  `connections.json` / `packages/` convention.

UI:

- **`AppMode.Assistant`** + new 🧠 Assistant toggle on the toolbar.
- **`Views/AssistantView`** (sidebar list + filter) + per-block editor
  pane on the right with: read-only `Id`, `Title`, `When`, `Priority`
  (NumericUpDown — empty = unset), `Tags` (comma-separated), `Includes`
  (comma-separated), and a markdown body in a monospace `TextBox`.
  Editor pane is hidden until a block is selected so the empty state
  shows only the description hint.
- **`Views/AssistantActionsView`** for the toolbar action strip:
  ➕ Add / 💾 Save / 🗑 Delete. Save is explicit (per-keystroke writes
  would thrash disk for the body); selection switches discard pending
  edits, same as Scripts/Forms switching.

Tests: 21 new in `KnowledgeBlockStoreTests` covering parse + serialize
round-trip, frontmatter ordering, CRLF tolerance, malformed /
unterminated frontmatter, tags + includes lowercasing + dedup, empty
list omission, priority parse-as-int with unparseable string falling
to null and surviving in the raw `Frontmatter` map. 285/285 green.

What's still missing to close the loop (next steps):

- **Picker.** Pure function: takes a context (loaded Blast facades + open
  script text), matches `when:` rules to find entry-point blocks, walks
  `includes:` transitively with cycle detection, sorts by `priority`,
  returns ordered list.
- **Prompt builder.** Composes a system prompt from picked blocks +
  Blast facade summaries from `LoadedReferenceCatalog`. First named
  operation hooks in here.
- **Demo blocks.** Ship a couple of example `.md` files alongside
  DemoScripts/DemoForms so a fresh install has something to look at.

### 2026-04-27 (cont. 4) — XmlDocReader (xmldoc → structured lookup)

Second foundation piece for the AI roadmap. Useful immediately for the
eventual editor-tooltip feature, and required-by-construction for the
AI assistant (signatures-without-descriptions are weak context;
signatures-plus-summaries are strong context).

- **`Engine/XmlDocReader.cs`**. Static `TryRead(dllPath)` finds the
  matching `.xml` file alongside a DLL (NuGet ships both side-by-side)
  and returns an `XmlDocSet?` (null if no doc file or malformed XML —
  callers shouldn't have to wrap it in a try). `Parse(xmlContent,
  fallbackAssemblyName)` exposed for callers that already have the XML
  string in hand.
- **`XmlDocEntry`** record per documented member: ECMA-335 member ID,
  summary, remarks, returns, parameter list. Whitespace from compiler-
  emitted multi-line tags collapses to single trimmed strings so the
  output reads like prose.
- **`XmlDocSet`** wraps the entry list with a fast `Find(memberId)`
  lookup keyed by the member ID string.

Tests: 6 in `XmlDocReaderTests`. No-xml-beside-dll → null; malformed
xml → null (not throw); summary/remarks/params/returns extraction
from a synthesized fixture; multi-line summary collapses to one line;
fallback assembly name when the `<assembly>` element is missing;
malformed `<member>` entries skipped not crashed; **round-trip
against the real `Acme.Domain.xml`** the SampleModels project ships
(skipped gracefully if that build hasn't run yet, runs by default
because TaskBlaster's csproj target builds SampleModels first).

239/239 tests green.

### 2026-04-27 (cont. 3) — Blast.PrimaryFacade convention + LoadedReferenceCatalog

Foundation work for the AI-assistant roadmap entry. Two pieces, both
useful immediately as diagnostics, both load-bearing for the eventual
AI context-builder.

Blast-family convention (lives in 6 separate repos):

- **`[AssemblyMetadata("Blast.PrimaryFacade", "FQN1,FQN2,...")]`** stamped
  on each Blast nuget's main assembly via `<AssemblyAttribute>` in the
  csproj (or, for GuiBlast which sets `GenerateAssemblyInfo=false`,
  via a hand-rolled `AssemblyAttributes.cs`). Names the canonical
  front-door type(s) so any consumer (an AI helper, an IDE, a doc
  generator) can identify entry points without scanning every public
  type.

  | Package | Front doors |
  |---|---|
  | UtilBlast 1.2.1 | `Tabular.Blast`, `UtilBlastFactory` |
  | AzureBlast 2.1.1 | `MssqlDatabase`, `AzureServiceBus`, `AzureTableStorage`, `AzureKeyVault` |
  | GuiBlast 2.1.1 | `Prompts`, `Forms.Rendering.DynamicForm` |
  | NetworkBlast 1.0.1 | `NetClient` |
  | SqliteBlast 1.0.1 | `SqliteStore`, `SqliteBlastFactory` |
  | SecretBlast 1.0.3 | `SecretVault` |

- Each Blast README grew a **🤖 AI assistants** section explaining the
  convention, listing the package's facades in a table, and showing the
  reflection snippet to read the value back. Patch-version bump on each
  (additive change, no API surface broken). All published to nuget.org.
- TaskBlaster's csproj bumped to consume the new versions.

`LoadedReferenceCatalog` (in TaskBlaster):

- **`Engine/LoadedReferenceCatalog.cs`** walks `AppDomain.GetAssemblies()`,
  filters out ghosts (file deleted out from under us — same hardening
  `ScriptBlaster.GetLoadableAssemblies` got this morning), and produces
  a `LoadedReference` record per loaded assembly: name, version,
  location, origin classification, primary facades, exported namespaces.
- **`LoadedReferenceOrigin`** enum: `Framework` (runtime BCL),
  `Application` (TaskBlaster's bin), `Blast` (carries the
  PrimaryFacade attribute), `External` (under our package store or in
  `IConfigStore.ExternalDlls`), `Other`. Classification order is
  attribute-first: a Blast nuget restored to `~/.nuget/packages/`
  reads as Blast, not Other.
- **Static `ReadPrimaryFacades(asm)`** so anything else (a future
  editor toolbar, a `references` terminal command) can parse facades
  from any assembly without instantiating the catalog.
- Registered as a singleton in `Program.cs`.

Tests: 8 new in `LoadedReferenceCatalogTests`. Snapshot non-empty +
all locations are real files; `System.Runtime` classified as
Framework; UtilBlast classified as Blast and surfaces both declared
facades; loose DLLs registered in `ExternalDlls` classify as External
even when their path is outside the package store; static
`ReadPrimaryFacades` parses comma-separated trimmed names; assembly
without the attribute returns empty. 233/233 green.

What this unlocks: when the AI assistant work starts, the "what's in
scope right now" question is already answered as a structured snapshot.
No further package updates needed; everything for AI context-building
is metadata-already-on-disk.

### 2026-04-27 (cont. 2) — External references (NuGet + loose DLL) tab

User-facing surface for loading third-party assemblies into the script
engine. Built specifically to handle the "private canonical-models nupkg"
case (a corp-internal package full of model classes that scripts need to
`using`), but applies to any net10.0-compatible nupkg or loose dll.

Storage + manager:

- **`Externals/` namespace** owns the load lifecycle. `NupkgImporter`
  opens a `.nupkg` as a zip (it just is one), reads the `.nuspec` for
  identity, picks the best TFM from `lib/` with precedence
  `net10.0 → net9.0 → net8.0 → net7.0 → net6.0 → netstandard2.1 →
  netstandard2.0`, extracts the chosen folder's DLLs into
  `~/.taskblaster/packages/<id>/<version>/` (wipes the destination
  first so re-imports stay clean).
- **`AssemblyValidator`** uses `MetadataLoadContext` (System.Reflection.Emit
  package) to inspect a candidate without polluting the AppDomain. Resolver
  is built from runtime BCL + AppDomain assemblies + sibling DLLs in the
  package's lib folder + every DLL from already-imported externals. Reports:
  TFM warning for `.NETFramework`, **error** for self-conflicts (same
  identity name, different version, against either AppDomain or other
  externals), **warning** for unresolved or version-skewed references.
- **`ExternalReferenceManager`** orchestrates. `LoadAll()` runs at startup
  and swallows per-entry failures into an error list (one bad DLL doesn't
  kill the app — startup logs them as `⚠ ...` to the terminal).
  `AddDll`/`AddPackage` validate first, only commit on no-errors unless
  `force: true`. After `Assembly.LoadFrom` we call `GetTypes()` to flush
  `ReflectionTypeLoadException` so type-load issues surface immediately.
- **`force: true` + already-loaded same-name DLL** stages the new entry
  in config without a successful live load, so the next launch picks up
  the upgraded version. Mirrors the existing Remove behavior — we already
  document that AppDomain unload isn't supported.

Config:

- `IConfigStore` grew `ExternalDlls` (`IList<string>`) and
  `ExternalPackages` (`IList<ExternalPackageRef>`). `ConfigStore`
  serializes both as nullable arrays so legacy configs still load.
  Malformed package entries (missing id/version) are silently dropped
  on load. All 4 test config stubs updated.

UI:

- `ConfigDialog` is now 720×540 with three tabs: **Display** (theme,
  highlighter, code folding), **Location** (scripts/forms/vault folders),
  **External** (NuGet + DLL lists with Add/Remove). Add buttons run a
  file picker (`*.nupkg` or `*.dll`), then route through the manager.
- `ExternalValidationDialog` (new) renders one section per validated
  DLL with color-coded ✗ / ⚠ icons, a "✓ no issues" line otherwise, and
  three buttons: **Cancel**, **Add anyway** (only when errors present,
  red background), **Add** (default, only when no errors). External-tab
  Add/Remove are immediate-effect — they don't participate in the
  Settings dialog's Save/Cancel split because nupkg extraction can't
  be rolled back.
- New `WarningBrush` / `Color.Warning` in both themes (amber, theme-tuned).
- New `ResourceKeyToBrushConverter` registered in `App.axaml` so issue
  rows can name their colour by string.

Bundled fixture:

- **`TaskBlaster.SampleModels/`** project produces `Acme.Domain.1.0.0.nupkg`
  with `Customer`/`Person`/`Order`/`Product`/`Address` records plus
  `SampleData` fixtures. Project name stays under the `TaskBlaster.*`
  prefix (it's our test fixture); the *output* package and assembly are
  named `Acme.Domain` to read like a real third-party dep. Built via
  `<AssemblyName>` + `<RootNamespace>` + `<PackageId>` overrides.
- TaskBlaster's csproj has a `BeforeTargets="AssignTargetPaths"` MSBuild
  target that builds the sibling project, copies the produced .nupkg into
  `TaskBlaster/DemoNugets/`, and adds it as Content via a
  *target-time* ItemGroup (avoids the chicken-and-egg on a fresh clone).
- First-run seeder creates `~/.taskblaster/demo-nugets/` and copies the
  bundled .nupkg there if missing; `--seed-demos` does the overwrite
  variant. Mirrors DemoScripts/DemoForms.
- `DemoScripts/acme-domain-demo.csx` walks the package's types via the
  `Blast` display DSL. Top of the file documents the two-step setup
  (Settings → External → Add the nupkg → restart).

Tests:

- 22 new tests across 4 files. **`NupkgImporterTests`** (6): identity
  parsing, TFM precedence, netstandard fallback, no-compatible-TFM throw,
  missing-nuspec throw, destination wipe. **`AssemblyValidatorTests`** (4):
  clean DLL, identity conflict, same-version no-conflict, unreadable path
  surfaces as error not throw. **`ExternalReferenceManagerTests`** (8):
  add/persist, missing file errors without persist, conflict + force=false
  doesn't persist, package extract+persist, same-id-different-version
  replaces config, RemovePackage cleans folder, RemoveDll drops entry,
  LoadAll collects errors. **`ConfigStoreExternalsTests`** (4): defaults,
  round-trip, legacy-config tolerance, malformed-entry filtering. New
  `ExternalsFixtures` helper synthesises throwaway DLLs via
  `PersistedAssemblyBuilder` (.NET 9+ persistent emit) and zips them into
  in-memory nupkgs — no binary fixtures committed to git.

Two production bugs the tests caught:

- **`ScriptBlaster.GetLoadableAssemblies()`** now skips assemblies whose
  backing file no longer exists. Without this, removing an external
  (or having one moved out from under us) would silently break every
  subsequent Roslyn compile because Roslyn re-reads `assembly.Location`
  from disk.
- **`ExternalReferenceManager.CommitDll`** persists on `force=true`
  even when live load fails. Surfaced by the upgrade-an-already-loaded-
  package case: without this, "Add anyway" did nothing (config not
  updated, restart didn't pick up the new version, user thought the
  feature was broken).

225/225 tests green.

### 2026-04-27 (cont.) — Editor + UX polish

Grab-bag pass over the editor and the surrounding chrome. Closes the
"Search / filter box on the Secrets DataGrid" item from the open list
by generalising it into a shared component used in three places.

Editor:

- **Switchable highlighter backend.** `EditorView.SetHighlighter("Native"
  | "TextMate")` toggles cleanly on the fly. Native uses
  AvaloniaEdit's xshd highlighter (lighter, scrolls noticeably smoother
  on larger files); TextMate keeps the VS Code Dark+/Light+ palette via
  TextMateSharp. New installs default to Native; existing configs without
  the key fall back to Native too. Plumbed through `IConfigStore.EditorHighlighter`,
  `ConfigDialog` (Editor highlighter combo + one-line hint), and
  `MainWindow` (applied at startup and re-applied on Settings save without
  reloading the open file).
- **Bundled native xshd files.** `Resources/Highlighting/CSharp.Dark.xshd`
  and `CSharp.Light.xshd` shipped as `EmbeddedResource`s; loaded on demand
  via `Assembly.GetManifestResourceStream` and `HighlightingLoader.Load`.
  Palettes follow VS Code Dark+/Light+: comments green, strings rust,
  numbers olive, methods sand, control-flow keywords magenta,
  declaration / built-in keywords blue. `ApplyTheme(ThemeVariant)` swaps
  the xshd file when the app theme flips so colours don't get stuck on
  the wrong variant.
- **Code folding (configurable).** `BraceFoldingStrategy` (inlined from
  the AvaloniaEdit demo project; the published nuget only ships the XML
  one) folds any `{ ... }` pair that crosses a newline. Wired via
  `EditorView.SetCodeFoldingEnabled(bool)` which installs / uninstalls
  `FoldingManager` on `_editor.TextArea` without recreating the editor.
  Persisted as `IConfigStore.CodeFolding` (default on) with a checkbox
  in `ConfigDialog`.
- **Wider line-number gutter.** Added an `ae:LineNumberMargin` style in
  `EditorView.axaml` with `Margin="4,0,8,0"` so the gutter doesn't crowd
  the first column of code, and the folding triangles have somewhere to
  sit.
- **Ctrl + mouse-wheel font zoom.** `EditorView` listens for
  `PointerWheelChangedEvent` with `Ctrl` held and routes to
  `ZoomIn` / `ZoomOut`. Clamped 8-36, default 13. Status bar reflects the
  current size live.

Toolbar / status bar:

- **Terminal panel toggle.** New right-side `ToggleSwitch` on the toolbar
  ("Terminal") next to the Settings button. `MainWindow.ApplyTerminalVisibility`
  caches the splitter row height when hidden so the next show restores
  the user's previous panel size. Persisted as
  `IConfigStore.TerminalVisible` (default on); legacy configs without
  the key load cleanly. `TerminalView` header text relabeled "Output" →
  "Terminal" to match the toggle.
- **Errors in red.** `StatusBarView` grew `SetStatus(text, StatusLevel)`
  with a `StatusLevel.Error` overload that pulls `DangerBrush` from the
  theme. `MainWindow` calls it with `StatusLevel.Error` on
  `BlastStatus.Error`, so a failed run no longer reads the same colour
  as "Ready".

Filter boxes (shared component):

- **`Views/FilterBoxView`.** Reusable inline filter (TextBox +
  PlaceholderText + × clear button + Esc-to-clear). `Matches(haystack)`
  is the canonical predicate every host re-uses: case-insensitive,
  whitespace-trimmed, all whitespace tokens must match (substring).
  `FilterChanged` event surfaces the trimmed text. Static
  `Matches(haystack, filter)` exposed for direct callers (DataGrid
  filtering).
- **Hosts.**
  - `SidebarView` caches `_allFiles` and re-applies the predicate on
    filter change.
  - `ConnectionsView` keeps an `_allNames` source list and rebuilds the
    bound `ObservableCollection<string>` on filter change.
  - `SecretsView` lets the filter override the category-sidebar
    selection while non-empty (matches across Category / Key /
    Description); falls back to the category selection when the filter
    clears.
- Saved a feedback memory: default to extracting shared / reusable
  components even when there's only one consumer, instead of waiting
  for rule-of-three.

### 2026-04-27 — Ribbon toolbar + connection priming + friendlier failure UX

A grab-bag day: the long-pending two-strip toolbar landed, the
ConnectionsResolver got tighter semantics so a connection declares its
own intent toward the vault, and runtime errors now render as a
collapsible red entry instead of a stack-trace dump.

Toolbar:

- **Two-strip ribbon.** `ToolbarView` is now a navigation header
  (mode toggles + Settings) on top, and a contextual `ContentPresenter`
  bottom strip whose contents change per mode. Bottom strip hidden in
  modes that don't need it.
- **Per-mode action surfaces** (`Views/ScriptFormActionsView.axaml`,
  `Views/SecretsActionsView.axaml`, `Views/ConnectionsActionsView.axaml`).
  Each is a small `UserControl` owning its events and enable-state.
  `MainWindow` swaps `Toolbar.ActionsContent` per mode; `SecretsView`
  and `ConnectionsView` expose their action panel as a `ToolbarActions`
  property and wire its events to existing in-view handlers.
- **Run/Preview gated on file selection.** Was unconditionally enabled
  on mode change; now stays disabled until a script/form is picked,
  greys back out when the file is deleted or the folder changes.
- **GridSplitter restyle.** Application-level `GridSplitter.niceV` /
  `niceH` classes give all four splitters a 6 px hit area with a 2 px
  centered bar, theme-aware accent (`AccentBrush`, swaps with the
  active theme) on `:pointerover`. Adjacent panel borders
  (`SidebarView`, `TerminalView`, `SecretsView` categories pane,
  `ConnectionsView` list pane) removed because they were doubling-up
  with the splitter line.

ConnectionsResolver semantics tightened:

- A declared connection is now **authoritative for its name**. Asking
  for a key it doesn't declare returns `string.Empty` without
  consulting the vault — a pure-plaintext connection (e.g. `formidable`
  with only `baseUrl`) no longer triggers a stray unlock prompt for an
  optional well-known key like `token`.
- **Connection-level vault priming.** If the connection has *any*
  `fromVault` field, the resolver primes the vault by resolving one of
  those refs the first time the connection is consulted, so the unlock
  prompt fires up-front instead of being deferred until a specific
  vault-backed field happens to be read. The intent signal is the
  connection's contents — no per-connection flag needed.
- Resolver tests grew two cases (priming touches vault on plaintext
  access; declared-connection + undeclared key returns empty without
  hitting vault). 6/6 in `ConnectionsResolverTests`, 203/203 overall.

Runtime-failure UX:

- **`BlastResult` grew a `Details` field.** `ScriptBlaster.RunAsync`
  now classifies common operational exceptions
  (`HttpRequestException` / `SocketException`, `TimeoutException`,
  `IOException` / `FileNotFoundException`, `UnauthorizedAccessException`)
  into a one-line summary like `Network: Connection refused (localhost:8383)`
  and packages the full `Exception.ToString()` in `Details`. Unknown
  exceptions still get the unmodified stack so genuine bugs surface
  in full.
- **`ErrorItem` in the terminal.** `MainWindow` calls
  `Terminal.LogError(summary, details)` on `BlastStatus.Error`; the
  terminal renders an Avalonia `Expander` with a red monospace header,
  a 📋 Copy button pinned in the header (always visible), and the
  full stack in a self-contained scroll region (`MaxHeight=240`,
  inner horizontal scroll) so long lines don't push the outer
  terminal's horizontal scroll and drag the header off-screen.
- The corresponding `RunAsync_RuntimeException_ReturnsError` test was
  updated to assert against `result.Message` / `result.Details` (the
  exception no longer streams to live `onOutput`).

Form designer fix:

- **Live label updates in `OptionsPropertyEditor`.** The options
  ListBox was bound to a frozen `List<string>` projection, so editing
  Value / Label at the bottom didn't update the displayed row text
  until a save / reload. Switched to a member-mutating
  `ObservableCollection<string>` and added `UpdateDisplayForSelected`
  calls from `CommitValueFromTextBox`, `CommitLabel`, and
  `OnValueComboSelectionChanged`.

### 2026-04-26 (cont. 3) — Multipart named connections

End-to-end vertical slice for the Connections feature. A connection
is a named bag of fields; each field is either a plaintext literal
(URL, server, account name, timeout) or a pointer into the vault
(token, password, secret key). Phases 1+2 of the original 3-phase
plan; Phase 3 (legacy import wizard) is in the open list and will
only get built if a real ScriptRunner.Plugins data set shows up.

Phase 1: model + resolver:

- **`TaskBlaster.Connections`** namespace: `Connection`,
  `ConnectionField`, `ConnectionVaultRef` records;
  `ConnectionFieldEditor` INPC viewmodel for the UI;
  `ConnectionSnapshot` (`DynamicObject`) for the resolved view scripts
  see; `ConnectionStore` (JSON-backed) reads/writes
  `~/.taskblaster/connections.json`; `ConnectionsResolver` wraps a
  vault resolver with a connections overlay.
- **Resolver semantics:** for each `(category, key)` lookup,
  `connections[category][key]` is consulted first. `{ "value": ... }`
  returns the literal (no vault unlock); `{ "fromVault": ... }`
  dispatches to the underlying vault resolver against the pointed-to
  pair. An absent connection or absent field falls through to the
  vault resolver directly so all-vault scripts keep working
  unchanged.
- **`ScriptSecrets`** grew an `IConnectionStore` ctor parameter and
  uses it to wrap the script-facing `Resolver`. New API:
  `Connections()` lists registered connection names;
  `GetConnection(name)` returns a `dynamic` snapshot
  (`var conn = Secrets.GetConnection("github"); var url = conn.baseUrl;`);
  `GetConnection<T>(name)` deserialises the snapshot into a record /
  class via JsonSerializer with case-insensitive name match and
  `JsonNumberHandling.AllowReadingFromString`.
- **DI:** `IConnectionStore` registered as a singleton in `Program.cs`,
  path anchored on `Path.GetDirectoryName(VaultFolder)` so the file
  follows the user when the TaskBlaster home moves.
- **Tests:** 7 in `ConnectionStoreTests` (round-trip, case-insensitive
  Get, sorted List, Remove, malformed-field drop, malformed-JSON
  recovery), 5 in `ConnectionsResolverTests` (plaintext / fromVault /
  missing-field fall-through / missing-connection fall-through /
  delegate shape), 8 in `ScriptSecretsConnectionsTests`
  (Connections() listing, plaintext-only no-unlock, mixed fields
  dereference vault, missing-name throws, Has / GetOrDefault, no-store
  empty case, dynamic member access, case-insensitive dynamic, typed
  record binding, numeric-from-string typed binding).

Phase 2: Connections tab:

- **`AppMode.Connections`** + `🔗 Connections` toolbar toggle.
- **`ConnectionsView`** with two-pane layout: name list on the left
  (`➕ Add` / `🗑 Delete`); per-connection editor on the right with a
  `DataGrid` of fields (Name / Mode / Value / ×) and an `➕ Add field`
  button. Mode column is a combo (`Plaintext` / `From vault`); Value
  column flips between a single TextBox (plaintext) and a paired
  category / key TextBox grid (from-vault) via INPC-driven
  `IsVisible` bindings on the `ConnectionFieldEditor` viewmodel.
- **Implicit persistence**: every name / mode / value edit calls
  `PersistCurrentConnection()` which writes the whole connection
  back through `IConnectionStore.Save`, mirroring the live-edit
  feel of the Secrets tab.

Other:

- **Settings dialog:** Theme moved out of the toolbar into a Theme
  dropdown at the top of Settings. `IThemeService.AvailableThemes`,
  `IConfigStore.Theme`, `App` now applies the persisted theme on
  startup. `🌓 Theme` toolbar button removed along with its event
  plumbing.
- **`DemoScripts/connections-demo.csx`** showing inventory + dynamic
  + typed forms, plus a commented-out NetworkBlast handoff. Bundled
  via the existing `DemoScripts/*.csx` content glob.
- **README** Stack list bumped to SecretBlast 1.0.2; demo table grew a
  connections-demo entry; new "Connections" section explaining the
  feature and the field convention.
- 202/202 TaskBlaster tests green.

### 2026-04-26 (cont. 2) — Category rename moves the secrets

`CategoriesDialog` rename now actually re-tags the affected secrets;
previously it only updated the picker list and required a per-secret
edit to follow.

- **`IVaultService.RenameCategoryAsync(oldName, newName)`** added.
  Walks the live envelopes, rewrites `category` on those that match
  case-insensitively (`OrdinalIgnoreCase`), saves under the same id so
  filenames stay opaque. Skips the catalog reserved id. Returns the
  rewrite count. Idempotent: re-running after a partial failure is safe
  because already-renamed secrets no longer match the old name.
- **`CategoriesDialog`** tracks a display-name → original-name map so
  add / rename / re-rename ops collapse into a clean list of
  `(OldName, NewName)` pairs at Save time. Fresh adds map to `null`
  (no envelope rewrite). The rename prompt now says "*N secret(s)
  currently use this category; they will be re-tagged to the new name
  when you save*" instead of the previous "edit them one by one"
  caveat.
- **`CategoriesDialogResult`** carries `Renames` alongside `Categories`.
- **`SecretsView.OnCategoriesClicked`** rewrites envelopes first, then
  flips the catalog list. The terminal log includes the re-tag count
  when non-zero.
- **4 new VaultService tests** covering: case-insensitive match across
  multiple secrets with id preservation, no-op when no match, no-op
  when old == new, and the contract that the catalog isn't touched
  (caller pairs the rename with `SetCategoriesAsync`).

### 2026-04-26 (cont.) — Form Settings polish + vault unlock fixes

Round of designer-UX cleanup followed by chasing the intermittent
"right password rejected" report.

- **Actions + Visibility editors → `DataGrid`.** Both views were
  hand-rolling rows in an `ItemsControl` with column definitions
  duplicated between header and rows; headers were drifting because
  `ItemsPresenter` introduces a small horizontal offset. Replaced with
  `DataGrid` + `DataGridTemplateColumn`s so the grid owns column
  alignment. Per-cell controls keep the always-editable feel via
  OneWay bindings + `TextChanged` / `IsCheckedChanged` /
  `SelectionChanged` handlers that look up the row's editor via
  `DataContext`.
- **`VisibilityRuleEditor.IsNeq` / `IsHide` mode flags.** The previous
  code inferred mode from "is `Neq` non-null?" / "is `Hide` non-empty?",
  which failed for new rules where both sides were empty (combo would
  show "not equal" but writes still went to `Eq`). Added explicit mode
  bits with `Value` / `TargetsCsv` accessors that route writes
  correctly; `FromDto` initialises the bits from whichever side of the
  toggle is populated, so loaded JSON behaves as before.
- **Form Settings tabs.** Added an explainer line under the Actions
  header. Fixed the Size tab so Width/Height labels sit *above* their
  inputs (they shared a row with auto-width columns before, putting
  them left of the boxes). Removed the `Dispatcher.Post(...
  DispatcherPriority.Loaded)` suppress flag in `SizeEditorView` that
  was eating the first toggle of "Allow user to resize" if the user
  clicked before the deferred reset ran; replaced with
  doc-equality guards in each `Commit*` method.
- **Status-bar dot + dividers.** Replaced the implicit
  filename-bullet dirty signal with an explicit `●` indicator in the
  right-hand status segments (left of the existing Ready/Running…
  status). Three states: `DangerBrush` (red) when dirty, `SuccessBrush`
  (green) when saved, muted when no file is open. Added a `Color.Success`
  to both themes plus a `SuccessBrush` semantic brush in `Base.axaml`.
  Switched all status-bar dividers from `SystemControlForegroundBaseMediumLowBrush`
  (no override; rendered invisible against our backgrounds) to
  `BorderBrush` so they actually show.
- **Vault unlock: NFC normalisation in SecretBlast.**
  `Argon2Kdf.DeriveAsync` now runs `password.Normalize(NormalizationForm.FormC)`
  before UTF-8 encoding so callers who type the same characters via
  different input methods (composed vs decomposed) derive the same key.
  ASCII passwords unaffected. Shipped as **SecretBlast 1.0.2**;
  TaskBlaster bumped to match.
- **Vault unlock: serialise concurrent attempts.** `VaultService` grew
  a `SemaphoreSlim(1, 1)` `_stateGate` that wraps `InitializeAsync`
  and `UnlockAsync`. Parallel clicks (or a click plus a script-triggered
  unlock) used to run `SecretVault.Open` twice with `_vault` still null
  on the second one, then both `AttachVault`-ed in some order with the
  loser's instance leaked but still wired to the `Locked` event. Late
  callers now also early-return when the vault is already open at the
  same path so the queued retry doesn't redo Argon2.
- **Vault unlock: "Verifying password…" busy state.**
  `SecretsView.SetVerifying(bool)` swaps the locked-panel hint and
  disables the Unlock + Reset buttons while `UnlockAsync` /
  `InitializeAsync` are running. Wired in `MainWindow.UnlockOrCreateVaultAsync`
  around both calls. Argon2 at 256 MiB / 3 / 4 takes 1-3 seconds; with
  no feedback the user couldn't tell the dialog had accepted the
  password and was re-clicking, spawning parallel chains.
- 176/176 TaskBlaster tests still green; 149/149 SecretBlast tests
  still green.

### 2026-04-26 — UtilBlast 1.1 + SqliteBlast 1.0 wired in

Two more siblings on the same day. Both bring real-world data tooling
to scripts without dragging in heavy dependencies.

- **UtilBlast 1.1.0** (in `~/Projects/UtilBlast/`): JSON ⇆ CSV bridge
  (`string.JsonToCsv()` / `string.CsvToJson()`, RFC 4180 compliant with
  quoted fields / embedded newlines / doubled quotes), `DataTable.ToCsv()` /
  `string.ParseCsvToDataTable()`, `IEnumerable<T>.ToCsv()` (reflection),
  `JObject.Flatten()`, `JToken.GetByPath("a.b[0].c")`. The pre-existing
  broken `DataTable.ToCsv(bool)` (no escaping) was replaced. 53 new tests,
  270/270 total green.
- **SqliteBlast 1.0.0** (in `~/Projects/SqliteBlast/`): brand-new Blast
  nuget for local SQLite. `ISqliteStore` with `Execute` / `ExecuteScalar<T>` /
  `Query<T>` (typed row mapping with full coercion) / `QueryDataTable` /
  `BeginTransaction` (rollback-on-dispose, commit explicitly). Directory-
  based migration runner with a `__migrations__` table for idempotence.
  Vault-aware `SetupAsync(resolver, name)` mirroring AzureBlast 2.1's pattern.
  Script-friendly `SqliteBlastFactory.Open(path)` and `InMemory()` factories.
  31 tests, 0 warnings.
- **TaskBlaster.csproj**: bumped `UtilBlast` 1.0.2 → 1.1.0; added `SqliteBlast 1.0.0`.
- **`Engine/ScriptBlaster.cs`**: force-loads `SqliteBlast.SqliteStore` alongside the others.
- **Demo scripts**:
  - `DemoScripts/sqlite-demo.csx` — in-memory store + parameter binding +
    transaction + typed Query, plus a commented vault-backed path.
  - `DemoScripts/json-csv-demo.csx` — JSON-to-CSV with nested flatten,
    CSV-to-JSON round-trip, `JObject.Flatten()`, `JToken.GetByPath()`.
- 176/176 TaskBlaster tests still green.

### 2026-04-26 — NetworkBlast + AzureBlast resolver path wired in

Two siblings landed on the same day; both consume `Secrets.Resolver`
(shape `Func<category, key, ct, Task<string>>`) so the vault stays the
single source of connection truth.

- **NetworkBlast 1.0.2** (in `~/Projects/NetworkBlast/`): brand-new
  Blast nuget for REST/HTTP/SOAP/OData. The 0.1 → 1.0 arc landed 2026-04-25;
  1.0.2 followed up with a small ergonomic fix (relaxed the `NetClient`
  resolver parameter from a custom `SecretResolver` delegate to plain
  `Func<string, string, CancellationToken, Task<string>>`) so
  `Secrets.Resolver` flows in directly without `.Invoke` or wrapper
  lambdas. 246 tests, 0 warnings under Release.
- **AzureBlast 2.1.0** (in `~/Projects/AzureBlast/`): purely-additive
  resolver path. Each component grew an async overload —
  `MssqlDatabase.SetupAsync(resolver, name)`,
  `AzureServiceBus.SetupAsync(...)`,
  `AzureTableStorage.InitializeAsync(...)`,
  `AzureKeyVault.InitializeKeyVaultAsync(...)` — that pulls connection
  values via the resolver. `AzureBlastOptions` gained `Resolver` plus
  `SqlConnectionName` / `ServiceBusConnectionName` /
  `TableConnectionName` / `KeyVaultConnectionName`; `AddAzureBlast`
  picks the resolver path when those names are set, falls back to the
  existing string path otherwise. Mix-and-match supported. 17 new
  resolver tests, all green.
- **TaskBlaster.csproj**: bumped `AzureBlast` 2.0.2 → 2.1.0; added
  `NetworkBlast 1.0.2`.
- **`Engine/ScriptBlaster.cs`**: force-loads `NetworkBlast.NetClient`
  alongside the other Blast assemblies so Roslyn picks it up via
  `AppDomain.GetAssemblies()`.
- **Demo scripts**:
  - `DemoScripts/network-demo.csx` — anonymous httpbin call plus a
    commented vault-backed (`new NetClient(Secrets.Resolver, "github")`)
    follow-up.
  - `DemoScripts/network-odata-demo.csx` — typed LINQ-flavored OData
    against the public Northwind service, demonstrating
    `FirstPageAsync()` + `IAsyncEnumerable<T>` auto-paging.
- 176/176 TaskBlaster tests still green.

### 2026-04-25 — Resizable forms (GuiBlast 2.1.0 + designer toggle)

GuiBlast 2.1.0 adds `FormSpec.Resizable`; TaskBlaster persists the bit
and exposes a checkbox in the designer.

- **GuiBlast 2.1.0** (in `~/Projects/GuiBlast/`): added
  `FormSpec.Resizable` (root-level JSON property `resizable`).
  `DynamicForm.ShowAsync` OR-merges it with the existing `canResize`
  argument so either spec or caller can opt in.
- **TaskBlaster.csproj**: bumped `GuiBlast` 2.0.0 → 2.1.0.
- **`FormEditor.Resizable`** (default false). Round-trips through
  `FormDto.Resizable` (nullable bool) — written as `"resizable": true`
  only when set; omitted otherwise.
- **`IFormDocument.Resizable`** + dirty-flag wiring in `FormDocument`,
  matching the `Width` / `Height` pattern.
- **Size tab UI**: a "Allow user to resize" checkbox under the
  Width/Height grid, with a small note that the option needs
  GuiBlast 2.1.0+. "Reset to auto" deliberately leaves the toggle
  alone (size and resizability are unrelated user choices).
- **3 new round-trip tests** in `FormEditorSchemaTests`. 176/176 green.
- Preview path needed no change; `MainWindow.PreviewFormAsync` already
  hands the JSON straight to `DynamicForm.ShowJsonAsync`, which now
  reads `spec.Resizable` itself.

### 2026-04-25 — Vault-backed form options (`optionsFrom` hint)

Select-style fields can now declare their options as vault keys in a
category. The designer offers a per-field Static / From-vault toggle;
TaskBlaster expands the JSON before handing it to GuiBlast, which stays
vault-agnostic.

- **`Forms/FormJsonExpander.ExpandAsync(json, vault)`** walks the JSON,
  finds `optionsFrom` hints, materialises options from
  `IVaultService.ListAsync` filtered by category, then strips the hint.
  Empty `options[]` → expander auto-fills all keys; pre-picked subset
  passes through verbatim. Forms with no hint round-trip unchanged.
- **`OptionsPropertyEditor`** in the designer: radio for Static / From
  vault. Vault mode keeps the options list visible (manual subset pick)
  but constrains Value to a ComboBox of vault keys. Auto-prefills label
  from key on first pick. Triggers the supplied `ensureUnlocked`
  callback when the vault is locked, so the user gets the standard
  password dialog instead of an empty list.
- **`FormDesignerView.Initialize(IVaultService, Func<CancellationToken,
  Task>)`** wires the vault and the unlock-on-demand callback through
  to the field editors.
- **`MainWindow.PreviewFormAsync`** runs `FormJsonExpander.ExpandAsync`
  before `DynamicForm.ShowJsonAsync` and only triggers an unlock when
  the JSON actually contains an `optionsFrom` hint.
- **`FormEditor.OptionsSourceEditor`** + `OptionsFromDto` persist the
  hint as `"optionsFrom": { "source": "vault", "category": "..." }`.
- **Demo**: `DemoForms/deploy.json` (vault-backed select + visibility
  rules).
- Tests: 5 in `FormJsonExpanderTests` (passthrough, materialisation,
  pre-picked subset, missing category, unknown source).

### 2026-04-25 — Graceful script abort on cancelled vault unlock

When a `.csx` script hits `Secrets.Resolve` against a locked vault and
the user cancels the unlock dialog, the script ends as `Cancelled`
rather than dumping a stack trace.

- **`IFriendlyScriptException`** marker interface in
  `Engine/ScriptExceptions.cs`. Implemented by `VaultLockedException
  : InvalidOperationException`.
- **`BlastResult.Cancelled(string? message)`** overload so the terminal
  can show a concise reason ("Vault is locked, cannot resolve secret.")
  without a stack dump. Status renders as ⊘, not ✗.
- **`ScriptBlaster.RunAsync`** catches `IFriendlyScriptException` and
  returns `Cancelled(ex.Message)`; other exceptions still go through
  the normal `Error` path with a stack trace.
- Tests: `ScriptSecretsTests.Script_WhenVaultStaysLocked_AbortsAsCancelled_WithoutStackDump`
  asserts both the status and the absence of a stack trace.

### 2026-04-25 — Dev / UX small batch

- **`--seed-demos` CLI flag.** `dotnet run --project TaskBlaster --
  --seed-demos` force-overwrites every shipped `DemoScripts/*.csx` and
  `DemoForms/*.json` into the user's configured folders. The first-run
  seeder still only copies *missing* files; this flag is the dev
  refresh path. Implemented in `Program.SeedDemos` + `ForceCopyDemos`.
- **Splash auto-advance.** `SplashWindow` now starts a 5-second
  `DispatcherTimer` and shows a small countdown ("starting in X
  seconds... or click to continue"). User click still skips immediately.
- **Sparse-punctuation pass on UI strings.** Em-dashes and stray commas
  before "or" / "and" stripped from terminal/log/dialog text. Memory
  rule recorded so the next pass stays consistent.
- **More demos.** Added `env-report.csx`, `inline-form.csx`,
  `secret-resolve.csx`, `vault-report.csx`, `quick-task-demo.csx`;
  added `DemoForms/deploy.json`, `peer.json`, `quick-task.json`.

### 2026-04-25 — Script-side vault access (`Secrets` global)

`.csx` scripts can now resolve vault entries directly:

```csharp
var token = Secrets.Resolve("api", "token");          // sync
var conn  = await Secrets.ResolveAsync("azure", "prod-sql"); // async
// Delegate form for libraries (AzureBlast / planned NetBlast / …):
var db = new SomeClient(Secrets.Resolver, "prod-sql");
```

- New `TaskBlaster.Engine.ScriptGlobals` is passed to Roslyn as the
  script-globals object; its public `Secrets` property (a
  `ScriptSecrets`) surfaces as a top-level identifier inside every
  script.
- `ScriptSecrets.Resolver` is a `Func<category, key, ct, Task<string>>`
  shaped for any third-party library that takes a named-connection
  resolver — no SecretBlast / TaskBlaster coupling on the library side.
- `IScriptBlaster.RunAsync` gained a `ScriptGlobals?` parameter; when
  non-null Roslyn is called with `globalsType: typeof(ScriptGlobals)`.
- If a script hits `Secrets.Resolve` against a locked vault,
  `MainWindow.EnsureVaultUnlockedAsync` hops to the UI thread and
  reuses the normal create/unlock dialog flow. Cancelling the prompt
  surfaces as a runtime `InvalidOperationException` inside the script.
- Demo: `DemoScripts/secret-resolve.csx`.
- Tests: 4 new (`ScriptSecretsTests`), 164 green. Script-touching
  tests moved into a shared `[Collection("ScriptBlaster")]` because
  `ScriptBlaster` swaps `Console.Out` globally and parallel tests were
  stomping on each other's captured output.

### 2026-04-24 — SecretBlast integration (Secrets tab)

SecretBlast 1.0.0 NuGet package is wired into TaskBlaster and live behind a
new 🔐 Secrets toolbar mode.

- **Envelope format.** Each SecretBlast secret is stored under an opaque
  32-char hex id; the *value* is a JSON envelope with `schemaVersion`,
  `category`, `key`, `value`, `description`, `createdUtc`, `updatedUtc`.
  Category and key names are encrypted at rest; nothing on disk leaks the
  organisational structure. Codec in `TaskBlaster/Secrets/SecretEnvelope.cs`,
  ids in `SecretId`.
- **`IVaultService`.** Stateful wrapper over `ISecretVault` that hides the
  envelope marshalling and exposes `category/key/value` CRUD plus
  `ResolveAsync(category, key)` for integrations. Registered as a singleton.
  Production KDF is 256 MiB / 3 / 4 (Argon2id); tests override to 1 MiB / 1 / 1
  so the suite stays under a second.
- **VaultFolder config.** New `IConfigStore.VaultFolder` property (default
  `~/.taskblaster/vault`), wired through `ConfigDialog` with a third folder
  row. Legacy configs without the field still load cleanly.
- **Unlock / create flow.** First time the user clicks 🔐 Secrets → Unlock,
  `MainWindow` detects whether `vault.json` exists at the configured path,
  then pops either a two-field `PasswordDialog` (create) or single-field
  (unlock, with retry on wrong password). `IPromptService` grew a
  `PasswordAsync(title, prompt, confirm)` method so the flow stays testable
  via `FakePromptService`.
- **Secrets UI.** `SecretsView` — category list on the left, a DataGrid
  on the right with Category / Key / Description / Updated columns. Toolbar
  actions inside the view: ➕ Add / ✏ Edit / 🗑 Delete / 📋 Copy value / 🔒 Lock.
  Add + Edit go through `SecretEntryDialog` with a 👁 value-reveal toggle
  and existing-category autocomplete. `Avalonia.Controls.DataGrid` 12.0.0
  package added and its Fluent style pulled into `App.axaml`.
- **139 tests green** — 11 new for the envelope codec, 13 for `VaultService`
  (round-trip, lock/unlock, rename keeps id, opaque filenames, resolve
  case-insensitive), 1 new for legacy-config load.

Still explicitly out of scope for this session: named-connection migration
for AzureBlast callers, bulk category rename, search box on the grid.

## Older

### 2026-04-24 — SecretBlast v0.1 implementation

Full crypto implementation landed on top of the stub. Still at
`~/Projects/SecretBlast/`, still not committed / published.

- **KDF:** `Konscious.Security.Cryptography.Argon2` 1.3.1, 32-byte key derivation.
- **Symmetric:** `System.Security.Cryptography.AesGcm`, 12-byte nonces, 16-byte tags.
- **Header canary** (`"canary-v1"`) encrypted with the derived key under
  `AAD = "SecretBlast" || vaultId || "canary"`. Wrong-password detection via
  `AuthenticationTagMismatchException` → `InvalidMasterPasswordException`.
- **Per-secret AAD** = `"SecretBlast" || vaultId || secretName` — swapping
  a `*.secret` file in from another vault fails authentication loudly.
- **Atomic writes** via `*.tmp` → `File.Move(overwrite: true)`.
- **Auto-lock timer** resets on every op; zero-length / infinite disables it.
- **New exceptions:** `VaultNotFoundException`, `VaultCorruptException`.
- Stub tests replaced with 27 real round-trip tests covering: full Create →
  Set → Close → Open → Unlock → Get cycle, wrong password, already-unlocked
  no-op, Lock event ordering, tampered ciphertext, cross-vault swap,
  auto-lock firing, atomic-write cleanup, header shape on disk.

Decisions taken during implementation (now in `DESIGN.md`):

- `UnlockAsync` on an already-unlocked vault is a **no-op** — no re-derive,
  no password revalidation. Callers holding the vault have already proven it.
- `Create` on a directory containing non-`vault.json` files is allowed.
- `Open` throws `VaultNotFoundException` eagerly (before unlock) so bad
  paths surface immediately.
- `CancellationToken` on `UnlockAsync` cancels the *wait* but not the
  in-flight Argon2 derivation (Konscious limitation).

### 2026-04-24 — SecretBlast stub

Scaffolded `~/Projects/SecretBlast/` as a standalone Blast nuget matching the
UtilBlast template. Public API surface + state machine + plaintext-filename
`ListAsync`. Crypto paths threw `NotImplementedException` (now replaced).

### 2026-04-24 — MEDI DI rollout

Migrated from hand-wired DI (only `IThemeService` threaded through constructors)
to `Microsoft.Extensions.DependencyInjection`.

- Added `Microsoft.Extensions.DependencyInjection` 10.0.7 package reference.
- `Program.BuildAvaloniaApp` now builds a `ServiceCollection`, registers
  singletons for `IThemeService`, `IConfigStore`, `IScriptBlaster`,
  `IPromptServiceFactory`, `IFormDocumentFactory`, and transients for `App`,
  `SplashWindow`, `MainWindow`. `App` is resolved from the provider.
- `App` and `SplashWindow` take `IServiceProvider` and resolve the next
  window from it — no more `new SplashWindow(themes)` / `new MainWindow(themes)`.
- `MainWindow` takes all its services via constructor injection; the
  `new ScriptBlaster()` / `new ConfigStore()` / `new AvaloniaPromptService(this)`
  field initializers are gone.
- Owner-window wrinkle solved via **option (2)**: `IPromptServiceFactory`
  registered as a singleton; `MainWindow` calls `Create(this)` in its ctor.
- `FormDocument.LoadFromFile` is now reached through `IFormDocumentFactory`,
  which also wraps `SaveToFile` so the cast in `MainWindow` is gone.
- 114/114 tests still pass.

Still explicitly out of scope (pull in when we actually need them):
view-models / MVVM, `Microsoft.Extensions.Hosting`, keyed services,
scoped lifetimes.
