using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using AgentBlast;
using AgentBlast.Interfaces;
using AgentBlast.Knowledge;
using AgentBlast.Prompts;
using Avalonia.Threading;
using TaskBlaster.Connections;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

/// <summary>
/// Script-scoped chat panel. Each turn auto-runs the picker against the
/// open script + loaded references, assembles the system prompt, sends
/// the conversation history to <see cref="AgentClient"/>, and appends the
/// response. Conversation state is per-script, kept in memory; switching
/// scripts swaps the visible history.
/// </summary>
public partial class ScriptChatView : UserControl
{
    private readonly TextBlock _header;
    private readonly TextBlock _status;
    private readonly TextBlock _contextHint;
    private readonly StackPanel _history;
    private readonly ScrollViewer _historyScroll;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;
    private readonly Button _clearButton;

    private IConfigStore? _config;
    private IConnectionStore? _connectionStore;
    private IKnowledgeBlockStore? _knowledge;
    private LoadedReferenceCatalog? _catalog;
    private AgentClient? _ai;
    private IVaultService? _vault;
    private Func<CancellationToken, Task>? _ensureVaultUnlocked;
    private Action<string>? _log;

    private string? _currentScriptPath;
    private Func<string>? _scriptTextProvider;

    /// <summary>Per-script chat history, keyed by absolute file path.</summary>
    private readonly Dictionary<string, List<AgentMessage>> _historyByScript = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Invisible padding at the bottom of the chat panel. Always lives
    /// as the last child of <see cref="_history"/>. ScrollToEnd targets
    /// the bottom of THIS, so the real copy button (sitting above it)
    /// is guaranteed to be inside the viewport — no more fighting
    /// layout-pass timing for the scroll position.
    /// </summary>
    private readonly Border _bottomSpacer = new() { Height = 60 };

    private CancellationTokenSource? _sendCts;

    public ScriptChatView()
    {
        InitializeComponent();
        _header        = this.FindControl<TextBlock>("HeaderText")!;
        _status        = this.FindControl<TextBlock>("StatusText")!;
        _contextHint   = this.FindControl<TextBlock>("ContextHint")!;
        _history       = this.FindControl<StackPanel>("HistoryPanel")!;
        _historyScroll = this.FindControl<ScrollViewer>("HistoryScroll")!;
        _inputBox      = this.FindControl<TextBox>("InputBox")!;
        _sendButton    = this.FindControl<Button>("SendButton")!;
        _clearButton   = this.FindControl<Button>("ClearButton")!;
    }

    /// <summary>Wire the view to its dependencies. Called once from MainWindow.</summary>
    public void Initialize(
        IConfigStore config,
        IConnectionStore connectionStore,
        IKnowledgeBlockStore knowledge,
        LoadedReferenceCatalog catalog,
        AgentClient ai,
        IVaultService vault,
        Func<CancellationToken, Task> ensureVaultUnlocked,
        Action<string> log)
    {
        _config = config;
        _connectionStore = connectionStore;
        _knowledge = knowledge;
        _catalog = catalog;
        _ai = ai;
        _vault = vault;
        _ensureVaultUnlocked = ensureVaultUnlocked;
        _log = log;
        UpdateForCurrentScript();
    }

    /// <summary>
    /// Called by MainWindow whenever the active script changes (or no
    /// script is selected). The provider is a closure that fetches the
    /// live editor text on demand — that way every turn sees the user's
    /// latest edits, not whatever was open when the chat started.
    /// </summary>
    public void SetCurrentScript(string? path, Func<string>? scriptTextProvider)
    {
        _currentScriptPath = path;
        _scriptTextProvider = scriptTextProvider;
        UpdateForCurrentScript();
    }

    private void UpdateForCurrentScript()
    {
        if (_currentScriptPath is null)
        {
            _header.Text = "🤖 Assistant";
            _contextHint.Text = string.Empty;
            _inputBox.IsEnabled = false;
            _sendButton.IsEnabled = false;
            _history.Children.Clear();
            RenderInfo("Open a script to start a conversation.");
            _history.Children.Add(_bottomSpacer);
            return;
        }

        _header.Text = $"🤖 Assistant — {Path.GetFileName(_currentScriptPath)}";
        _inputBox.IsEnabled = true;
        _sendButton.IsEnabled = true;
        RenderHistory(GetHistory(_currentScriptPath));
        UpdateContextHint();
    }

    private List<AgentMessage> GetHistory(string path)
    {
        if (!_historyByScript.TryGetValue(path, out var list))
        {
            list = new List<AgentMessage>();
            _historyByScript[path] = list;
        }
        return list;
    }

    private void RenderHistory(IReadOnlyList<AgentMessage> history)
    {
        // Clear() drops the spacer too — RenderMessage re-pins it via
        // PinSpacerToEnd, but we add it back here for the empty-state
        // path so the layout stays predictable.
        _history.Children.Clear();
        if (history.Count == 0)
        {
            RenderInfo("No messages yet. Ask anything about the open script.");
            _history.Children.Add(_bottomSpacer);
            return;
        }
        foreach (var m in history)
            RenderMessage(m);
        ScrollToBottom();
    }

    private void RenderInfo(string text)
    {
        _history.Children.Add(new TextBlock
        {
            Text = text,
            FontStyle = FontStyle.Italic,
            Opacity = 0.6,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
    }

    private void RenderMessage(AgentMessage m)
    {
        var isUser = string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);
        // User bubble: plain text — they typed it, no markdown intended.
        // Assistant bubble: render markdown so headings / code / tables /
        // bold appear as the model meant them.
        Control body = isUser
            ? new SelectableTextBlock
            {
                Text = m.Content,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 12,
            }
            : MarkdownRenderer.Render(m.Content);

        // The assistant bubble keeps its XAML-bound DynamicResource
        // brushes (works on theme switch). The user bubble overrides
        // them in code with the resolved AccentBrush + a contrasting
        // foreground. Resolution happens here in ScriptChatView (which
        // IS in the visual tree, so resource lookup actually finds the
        // theme brushes — the bubble's own context fails the lookup).
        IBrush? userAccent = isUser ? FindBrush("AccentBrush") : null;
        IBrush? userFg     = isUser ? FindBrush("BgBrush")     : null;

        var bubble = new ChatBubbleView();
        _history.Children.Add(bubble);
        bubble.SetContent(body, isUser, userAccent, userFg);

        // Per-bubble copy button — separate StackPanel sibling right
        // after the bubble. Copies just THIS bubble's raw markdown.
        var copyButton = new Button
        {
            Content = "📋 Copy",
            FontSize = 11,
            Padding = new Thickness(8, 2),
            MinHeight = 0,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(0, 2, 0, 0),
            [ToolTip.TipProperty] = "Copy this message to the clipboard",
        };
        var capturedContent = m.Content;
        copyButton.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            await clipboard.SetTextAsync(capturedContent);
            SetStatus("Copied to clipboard.", isError: false);
        };
        _history.Children.Add(copyButton);

        // Re-pin the ghost spacer to the very end so ScrollToEnd has a
        // stable, never-clipped scroll target below the real copy button.
        PinSpacerToEnd();

        // Persistent SizeChanged listener for ~1.5s — each layout pass
        // (nested tables, code blocks) re-asserts the bottom. The
        // spacer guarantees the copy button is above where we scroll to.
        var deadline = DateTime.UtcNow.AddSeconds(1.5);
        EventHandler<SizeChangedEventArgs>? onPanelGrew = null;
        onPanelGrew = (_, _) =>
        {
            if (DateTime.UtcNow > deadline)
            {
                _history.SizeChanged -= onPanelGrew!;
                return;
            }
            _historyScroll.ScrollToEnd();
        };
        _history.SizeChanged += onPanelGrew;
        _historyScroll.ScrollToEnd();
    }

    private void PinSpacerToEnd()
    {
        // Move the spacer to be the LAST child of the chat history. It's
        // an invisible Border — provides 60 px of dead space below the
        // latest copy button so ScrollToEnd has somewhere safe to land
        // (the bottom of the spacer = viewport bottom = copy button is
        // 60 px above the viewport bottom = always visible).
        if (_history.Children.Contains(_bottomSpacer))
            _history.Children.Remove(_bottomSpacer);
        _history.Children.Add(_bottomSpacer);
    }

    private IBrush? FindBrush(string key)
        => this.TryFindResource(key, out var v) && v is IBrush b ? b : null;

    private void ScrollToBottom()
    {
        // No-op now — RenderMessage uses BringIntoView on each bubble,
        // which is more reliable than computing scroll positions
        // ourselves. Kept as a method so existing call sites still work.
    }

    private void UpdateContextHint()
    {
        if (_knowledge is null || _catalog is null || _currentScriptPath is null)
        {
            _contextHint.Text = string.Empty;
            return;
        }
        var ctx = BuildPickerContext();
        var picked = KnowledgeBlockPicker.Pick(_knowledge.List(), ctx);
        var refs = _catalog.Snapshot()
            .Count(r => r.Origin is LoadedReferenceOrigin.Blast or LoadedReferenceOrigin.External);
        _contextHint.Text = $"context: {picked.Count} block(s), {refs} ref(s)";
    }

    private PickerContext BuildPickerContext()
    {
        var refs = _catalog!.Snapshot();
        var typeFqns = new HashSet<string>(StringComparer.Ordinal);
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in refs)
        {
            foreach (var fqn in r.PrimaryFacades) typeFqns.Add(fqn);
            foreach (var ns in r.Namespaces) namespaces.Add(ns);
        }
        return new PickerContext(typeFqns, namespaces, Array.Empty<string>());
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            _ = SendCurrentAsync();
        }
    }

    private async void OnSendClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SendCurrentAsync();

    private async Task SendCurrentAsync()
    {
        if (_currentScriptPath is null) return;
        if (_ai is null || _config is null || _connectionStore is null
            || _knowledge is null || _catalog is null || _vault is null) return;

        var question = (_inputBox.Text ?? string.Empty).Trim();
        if (question.Length == 0) return;

        var providerName = _config.AiDefaultProvider;
        if (string.IsNullOrEmpty(providerName))
        {
            SetStatus("No AI provider configured. Open Settings → AI to pick one.", isError: true);
            return;
        }
        var conn = _connectionStore.Get(providerName);
        if (conn is null)
        {
            SetStatus($"AI provider '{providerName}' not found in connections.", isError: true);
            return;
        }

        // If any field on the connection is vault-backed, ensure the vault
        // is unlocked before we make the call — same pattern the Settings
        // Test button uses.
        if (_ensureVaultUnlocked is not null && conn.Fields.Values.Any(f => f.FromVault is not null))
        {
            try { await _ensureVaultUnlocked(CancellationToken.None); }
            catch { /* user cancelled the unlock */ }
            if (!_vault.IsUnlocked)
            {
                SetStatus("Vault stays locked — can't read API key.", isError: true);
                return;
            }
        }

        var history = GetHistory(_currentScriptPath);

        // The first user message of a session inlines the script as
        // context so the model has something concrete to refer back to;
        // subsequent turns assume the model still remembers.
        string userContent;
        if (history.Count == 0)
        {
            var scriptText = _scriptTextProvider?.Invoke() ?? string.Empty;
            userContent = BuildFirstTurnUserMessage(_currentScriptPath, scriptText, question);
        }
        else
        {
            userContent = question;
        }

        history.Add(AgentMessage.User(userContent));
        // Render the user's *typed* question rather than the script-laden
        // first-turn payload — the giant code dump is for the model, not
        // the visual transcript.
        RenderMessage(AgentMessage.User(question));
        ScrollToBottom();
        _inputBox.Text = string.Empty;

        var ctx = BuildPickerContext();
        var refs = _catalog.Snapshot();
        var picked = KnowledgeBlockPicker.Pick(_knowledge.List(), ctx);
        var prompt = PromptBuilder.Build(picked, refs, userMessage: string.Empty);

        SetBusy(true);
        SetStatus($"Asking… ({picked.Count} block(s), {history.Count} turn(s))", isError: false);

        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        AgentCompletionResult result;
        try
        {
            result = await _ai.SendAsync(providerName, prompt.SystemMessage, history, _sendCts.Token);
        }
        catch (Exception ex)
        {
            result = AgentCompletionResult.Fail(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        if (!result.Success)
        {
            // Roll back the user turn so the next attempt doesn't ship a
            // duplicated message into the conversation.
            if (history.Count > 0 && string.Equals(history[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
                history.RemoveAt(history.Count - 1);
            SetStatus($"Failed: {result.Error}", isError: true);
            _log?.Invoke($"AI send failed: {result.Error}");
            return;
        }

        history.Add(AgentMessage.Assistant(result.Text!));
        RenderMessage(AgentMessage.Assistant(result.Text!));
        ScrollToBottom();
        var ms = result.Latency?.TotalMilliseconds ?? 0;
        var truncated = string.Equals(result.StopReason, "max_tokens", StringComparison.OrdinalIgnoreCase);
        var status = truncated
            ? $"Truncated at max_tokens after {ms:0} ms — raise 'maxTokens' on the provider connection."
            : $"Done in {ms:0} ms ({picked.Count} block(s) used).";
        SetStatus(status, isError: truncated);
        UpdateContextHint();
    }

    private static string BuildFirstTurnUserMessage(string path, string scriptText, string question)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("File: ").Append(Path.GetFileName(path)).Append("\n\n");
        sb.Append("```csharp\n");
        sb.Append(scriptText.TrimEnd('\r', '\n'));
        sb.Append("\n```\n\n");
        sb.Append("Question: ").Append(question);
        return sb.ToString();
    }

    private void SetBusy(bool busy)
    {
        _sendButton.IsEnabled = !busy;
        _inputBox.IsEnabled = !busy;
    }

    private void SetStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.Foreground = isError
            ? this.FindResource("DangerBrush") as IBrush
            : null;
    }

    private void OnClearClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentScriptPath is null) return;
        _historyByScript.Remove(_currentScriptPath);
        RenderHistory(Array.Empty<AgentMessage>());
        SetStatus("Conversation cleared.", isError: false);
    }
}
