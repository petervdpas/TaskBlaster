using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TaskBlaster.Ai;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;
using TaskBlaster.Knowledge;
using TextMateSharp.Grammars;

namespace TaskBlaster.Views;

/// <summary>
/// Two-pane editor for Directed-AI knowledge blocks. Sidebar lists every
/// block in the knowledge folder; the right pane edits id/title/when/priority
/// plus the markdown body. Save is explicit (the body is too noisy for
/// per-keystroke writes); Add/Delete go through the toolbar action strip.
/// </summary>
public partial class AssistantView : UserControl
{
    private readonly ListBox _list;
    private readonly TextBlock _header;
    private readonly TextBlock _idLabel;
    private readonly TextBox _titleBox;
    private readonly TextBox _whenBox;
    private readonly NumericUpDown _priorityBox;
    private readonly TextBox _tagsBox;
    private readonly TextBox _includesBox;
    private readonly TextEditor _bodyEditor;
    private readonly Grid _metadataGrid;
    private readonly Border _bodyBorder;
    private RegistryOptions? _tmRegistry;
    private TextMate.Installation? _tmInstallation;
    private readonly FilterBoxView _filter;
    private readonly AssistantActionsView _toolbarActions;

    private IKnowledgeBlockStore? _store;
    private IPromptService? _prompts;
    private Action<string>? _log;
    private LoadedReferenceCatalog? _catalog;
    private PromptArtifactWriter? _artifacts;

    private readonly List<KnowledgeBlock> _allBlocks = new();
    private readonly ObservableCollection<BlockListItem> _visible = new();
    private string? _selectedId;
    private bool _suppressDirty;
    private bool _isDirty;

    public AssistantView()
    {
        InitializeComponent();
        _list         = this.FindControl<ListBox>("BlocksList")!;
        _header       = this.FindControl<TextBlock>("Header")!;
        _idLabel      = this.FindControl<TextBlock>("IdLabel")!;
        _titleBox     = this.FindControl<TextBox>("TitleBox")!;
        _whenBox      = this.FindControl<TextBox>("WhenBox")!;
        _priorityBox  = this.FindControl<NumericUpDown>("PriorityBox")!;
        _tagsBox      = this.FindControl<TextBox>("TagsBox")!;
        _includesBox  = this.FindControl<TextBox>("IncludesBox")!;
        _bodyEditor   = this.FindControl<TextEditor>("BodyEditor")!;
        _metadataGrid = this.FindControl<Grid>("MetadataGrid")!;
        _bodyBorder   = this.FindControl<Border>("BodyBorder")!;
        _filter       = this.FindControl<FilterBoxView>("Filter")!;
        _filter.FilterChanged += (_, _) => ApplyFilter();
        _list.ItemsSource = _visible;

        _bodyEditor.Document ??= new TextDocument();
        _bodyEditor.Document.TextChanged += (_, _) => MarkDirty();

        InstallMarkdownHighlighter(CurrentTmTheme());
        ActualThemeVariantChanged += (_, _) => ApplyMarkdownTheme();

        _toolbarActions = new AssistantActionsView();
        _toolbarActions.AddClicked     += OnAddClicked;
        _toolbarActions.SaveClicked    += OnSaveClicked;
        _toolbarActions.DeleteClicked  += OnDeleteClicked;
        _toolbarActions.PreviewClicked += OnPreviewClicked;
    }

    private ThemeName CurrentTmTheme() =>
        ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;

    private void InstallMarkdownHighlighter(ThemeName theme)
    {
        _tmInstallation?.Dispose();
        _tmRegistry = new RegistryOptions(theme);
        _tmInstallation = _bodyEditor.InstallTextMate(_tmRegistry);
        _tmInstallation.SetGrammar(_tmRegistry.GetScopeByLanguageId("markdown"));
    }

    private void ApplyMarkdownTheme()
    {
        if (_tmInstallation is null || _tmRegistry is null) return;
        _tmInstallation.SetTheme(_tmRegistry.LoadTheme(CurrentTmTheme()));
    }

    /// <summary>The action strip this view contributes to the main toolbar.</summary>
    public Control ToolbarActions => _toolbarActions;

    /// <summary>Wire the view to its dependencies.</summary>
    public void Initialize(
        IKnowledgeBlockStore store,
        IPromptService prompts,
        Action<string> log,
        LoadedReferenceCatalog catalog,
        PromptArtifactWriter artifacts)
    {
        _store = store;
        _prompts = prompts;
        _log = log;
        _catalog = catalog;
        _artifacts = artifacts;
        Reload();
    }

    /// <summary>Re-read the store and rebuild the list. Preserves the current selection if it still exists.</summary>
    public void Reload()
    {
        if (_store is null) return;
        _store.Reload();
        var keepId = _selectedId;

        _allBlocks.Clear();
        _allBlocks.AddRange(_store.List());
        ApplyFilter();

        if (keepId is not null)
        {
            var match = _visible.FirstOrDefault(item => string.Equals(item.Id, keepId, StringComparison.OrdinalIgnoreCase));
            _list.SelectedItem = match;
        }
        else
        {
            _list.SelectedItem = null;
        }
    }

    private void ApplyFilter()
    {
        _visible.Clear();
        foreach (var b in _allBlocks)
        {
            if (_filter.Matches(b.Title) || _filter.Matches(b.Id))
                _visible.Add(new BlockListItem(b.Id, b.Title));
        }
    }

    private void OnBlockSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var item = _list.SelectedItem as BlockListItem;
        // Discard pending edits on selection change — explicit Save is the
        // only way to keep changes; this matches the Scripts/Forms switch
        // behaviour and avoids the "unsaved changes" prompt churn.
        _selectedId = item?.Id;
        _toolbarActions.HasSelection = item is not null;
        LoadIntoEditor(item?.Id);
    }

    private void LoadIntoEditor(string? id)
    {
        _suppressDirty = true;
        try
        {
            if (id is null || _store is null)
            {
                _header.Text = "Pick a block on the left, or add a new one.";
                _idLabel.Text = string.Empty;
                _titleBox.Text = string.Empty;
                _whenBox.Text = string.Empty;
                _priorityBox.Value = null;
                _tagsBox.Text = string.Empty;
                _includesBox.Text = string.Empty;
                SetBodyText(string.Empty);
                SetEditorEnabled(false);
                MarkClean();
                return;
            }

            var block = _store.Get(id);
            if (block is null)
            {
                // Block vanished (deleted on disk between reload and selection).
                _header.Text = $"Block '{id}' not found.";
                SetEditorEnabled(false);
                MarkClean();
                return;
            }

            _header.Text = block.Title;
            _idLabel.Text = block.Id;
            _titleBox.Text = block.Title;
            _whenBox.Text = block.Frontmatter.TryGetValue("when", out var w) ? w : string.Empty;
            _priorityBox.Value = block.Priority.HasValue ? new decimal(block.Priority.Value) : null;
            _tagsBox.Text = string.Join(", ", block.Tags);
            _includesBox.Text = string.Join(", ", block.Includes);
            SetBodyText(block.Body);
            SetEditorEnabled(true);
            MarkClean();
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void SetEditorEnabled(bool enabled)
    {
        _metadataGrid.IsVisible = enabled;
        _bodyBorder.IsVisible = enabled;
    }

    /// <summary>
    /// Set the body text without firing the dirty signal — used for both
    /// the programmatic load (loading a selected block) and the cleared
    /// state (no selection). The TextEditor's Document.TextChanged fires
    /// for programmatic writes too, so we wrap the assignment in the
    /// suppress flag the rest of the editor already honours.
    /// </summary>
    private void SetBodyText(string value)
    {
        var prev = _suppressDirty;
        _suppressDirty = true;
        try
        {
            _bodyEditor.Document.Text = value;
        }
        finally
        {
            _suppressDirty = prev;
        }
    }

    private void OnEditorChanged(object? sender, TextChangedEventArgs e) => MarkDirty();

    private void OnPriorityChanged(object? sender, NumericUpDownValueChangedEventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        if (_suppressDirty) return;
        if (_selectedId is null) return;
        if (_isDirty) return;
        _isDirty = true;
        _toolbarActions.CanSave = true;
        _header.Text = (_titleBox.Text ?? string.Empty) + " *";
    }

    private void MarkClean()
    {
        _isDirty = false;
        _toolbarActions.CanSave = false;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_store is null || _prompts is null) return;

        var raw = await _prompts.InputAsync(
            "New knowledge block",
            "Block id (used as the file name, e.g. 'mssql-rules'):",
            defaultValue: "");
        if (string.IsNullOrWhiteSpace(raw)) return;

        var id = SanitizeId(raw);
        if (string.IsNullOrEmpty(id))
        {
            await _prompts.MessageAsync("Invalid id", "Id can only contain letters, digits, '-' and '_'.");
            return;
        }

        if (_store.Get(id) is not null)
        {
            await _prompts.MessageAsync("Already exists", $"A block with id '{id}' already exists.");
            return;
        }

        var block = new KnowledgeBlock(
            id,
            KnowledgeBlockStore.Humanise(id),
            string.Empty,
            Priority: null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        _store.Save(block);
        _log?.Invoke($"Knowledge block '{id}' created.");
        Reload();

        var match = _visible.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        _list.SelectedItem = match;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_store is null || _selectedId is null) return;

        var fm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Preserve any extra frontmatter keys the user added by hand.
        var existing = _store.Get(_selectedId);
        if (existing is not null)
        {
            foreach (var (k, v) in existing.Frontmatter)
            {
                if (IsManagedKey(k)) continue;
                fm[k] = v;
            }
        }

        var when = (_whenBox.Text ?? string.Empty).Trim();
        if (when.Length > 0) fm["when"] = when;

        var title = string.IsNullOrWhiteSpace(_titleBox.Text)
            ? KnowledgeBlockStore.Humanise(_selectedId)
            : _titleBox.Text.Trim();

        int? priority = _priorityBox.Value.HasValue ? (int)_priorityBox.Value.Value : null;
        var tags = KnowledgeBlockStore.ParseList(_tagsBox.Text);
        var includes = KnowledgeBlockStore.ParseList(_includesBox.Text);

        var block = new KnowledgeBlock(_selectedId, title, _bodyEditor.Document.Text ?? string.Empty, priority, tags, includes, fm);
        _store.Save(block);

        _log?.Invoke($"Knowledge block '{_selectedId}' saved.");

        // Refresh in-place: update the visible item title without losing focus on the editor.
        var idx = _visible.ToList().FindIndex(item => string.Equals(item.Id, _selectedId, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _visible[idx] = new BlockListItem(_selectedId, title);
        var allIdx = _allBlocks.FindIndex(b => string.Equals(b.Id, _selectedId, StringComparison.OrdinalIgnoreCase));
        if (allIdx >= 0) _allBlocks[allIdx] = block;

        _header.Text = title;
        MarkClean();
    }

    private void OnPreviewClicked(object? sender, EventArgs e)
    {
        if (_store is null || _catalog is null || _artifacts is null) return;

        var refs = _catalog.Snapshot();

        // Build the picker context from the live catalog. The picker matches
        // type-FQN rules against PrimaryFacades (the front doors blocks
        // would actually name) — random internal types aren't useful as
        // match targets and would only inflate the set.
        var typeFqns = new HashSet<string>(StringComparer.Ordinal);
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in refs)
        {
            foreach (var fqn in r.PrimaryFacades) typeFqns.Add(fqn);
            foreach (var ns in r.Namespaces) namespaces.Add(ns);
        }
        var ctx = new PickerContext(typeFqns, namespaces, Array.Empty<string>());

        var allBlocks = _store.List();
        var picked = KnowledgeBlockPicker.PickWithReasons(allBlocks, ctx);
        var pickedOnly = picked.Select(p => p.Block).ToList();
        var prompt = PromptBuilder.Build(pickedOnly, refs, userMessage: string.Empty);

        var path = _artifacts.Write("preview", ctx, refs, picked, prompt);
        _log?.Invoke($"Preview: {picked.Count}/{allBlocks.Count} block(s) picked. Wrote {path}");
        foreach (var p in picked)
            _log?.Invoke($"  • {p.Block.Id} — {p.Reason}");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_store is null || _prompts is null) return;
        var id = _selectedId;
        if (id is null) return;

        var ok = await _prompts.ConfirmAsync(
            "Delete knowledge block",
            $"Delete block '{id}'? This removes the markdown file from disk.");
        if (!ok) return;

        _store.Delete(id);
        _log?.Invoke($"Knowledge block '{id}' deleted.");
        _selectedId = null;
        _toolbarActions.HasSelection = false;
        // Clear the editor up front: clearing the ListBox via Reload doesn't
        // reliably re-fire SelectionChanged when the selected item is the one
        // that disappeared from the source collection.
        LoadIntoEditor(null);
        Reload();
    }

    private static bool IsManagedKey(string k) =>
        string.Equals(k, "title",    StringComparison.OrdinalIgnoreCase)
     || string.Equals(k, "when",     StringComparison.OrdinalIgnoreCase)
     || string.Equals(k, "priority", StringComparison.OrdinalIgnoreCase)
     || string.Equals(k, "tags",     StringComparison.OrdinalIgnoreCase)
     || string.Equals(k, "includes", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeId(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^3];
        // Lowercase + replace whitespace with '-'; reject anything outside [a-z0-9-_].
        var lower = trimmed.ToLowerInvariant().Replace(' ', '-');
        foreach (var c in lower)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return string.Empty;
        }
        return lower;
    }

    private sealed record BlockListItem(string Id, string Title)
    {
        public override string ToString() => Title;
    }
}
