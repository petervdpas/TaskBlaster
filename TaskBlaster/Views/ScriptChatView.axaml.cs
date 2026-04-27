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
using Avalonia.Threading;
using TaskBlaster.Ai;
using TaskBlaster.Connections;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;
using TaskBlaster.Knowledge;

namespace TaskBlaster.Views;

/// <summary>
/// Script-scoped chat panel. Each turn auto-runs the picker against the
/// open script + loaded references, assembles the system prompt, sends
/// the conversation history to <see cref="AiClient"/>, and appends the
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
    private AiClient? _ai;
    private IVaultService? _vault;
    private Func<CancellationToken, Task>? _ensureVaultUnlocked;
    private Action<string>? _log;

    private string? _currentScriptPath;
    private Func<string>? _scriptTextProvider;

    /// <summary>Per-script chat history, keyed by absolute file path.</summary>
    private readonly Dictionary<string, List<AiMessage>> _historyByScript = new(StringComparer.OrdinalIgnoreCase);

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
        AiClient ai,
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
            return;
        }

        _header.Text = $"🤖 Assistant — {Path.GetFileName(_currentScriptPath)}";
        _inputBox.IsEnabled = true;
        _sendButton.IsEnabled = true;
        RenderHistory(GetHistory(_currentScriptPath));
        UpdateContextHint();
    }

    private List<AiMessage> GetHistory(string path)
    {
        if (!_historyByScript.TryGetValue(path, out var list))
        {
            list = new List<AiMessage>();
            _historyByScript[path] = list;
        }
        return list;
    }

    private void RenderHistory(IReadOnlyList<AiMessage> history)
    {
        _history.Children.Clear();
        if (history.Count == 0)
        {
            RenderInfo("No messages yet. Ask anything about the open script.");
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

    private void RenderMessage(AiMessage m)
    {
        var isUser = string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);
        var bg = isUser
            ? TryBrush("AccentBrush")
            : TryBrush("SurfaceBrush");
        var fg = isUser
            ? TryBrush("BgBrush") ?? TryBrush("TextPrimaryBrush")  // contrast against accent
            : TryBrush("TextPrimaryBrush");

        // User bubble: plain text — they typed it, no markdown intended.
        // Assistant bubble: render markdown so headings / code / tables /
        // bold appear as the model meant them. Both keep the raw text on
        // hand for the copy button.
        Control body = isUser
            ? BuildPlainBody(m.Content, fg)
            : BuildMarkdownBody(m.Content, fg);

        var copyButton = new Button
        {
            Content = "📋 Copy",
            FontSize = 10,
            Padding = new Thickness(6, 1),
            MinHeight = 0,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = fg,
            Opacity = 0.55,
            [ToolTip.TipProperty] = "Copy raw markdown to clipboard",
        };
        // Always copy the RAW markdown text, even though the assistant
        // body is a rendered tree — the rendered tree is the preview, the
        // markdown source is what's useful to paste elsewhere.
        copyButton.Click += async (_, _) => await CopyToClipboardAsync(m.Content);

        // Two-row grid so the button lives BELOW the text instead of
        // overlaying it — keeps it clear of the chat's vertical scrollbar
        // and never sits on top of message content.
        var bubbleContent = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(body,       0);
        Grid.SetRow(copyButton, 1);
        bubbleContent.Children.Add(body);
        bubbleContent.Children.Add(copyButton);

        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            // Assistant bubble fills the available column; user bubble
            // hugs the right and caps so short asks don't span the whole
            // panel like a banner.
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
            MaxWidth = isUser ? 320 : double.PositiveInfinity,
            Background = bg,
            BorderBrush = TryBrush("BorderBrush"),
            BorderThickness = isUser ? new Thickness(0) : new Thickness(1),
            Child = bubbleContent,
        };
        _history.Children.Add(border);
    }

    private static Control BuildPlainBody(string text, IBrush? fg)
    {
        return new SelectableTextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 12,
            Foreground = fg,
        };
    }

    private static Control BuildMarkdownBody(string markdown, IBrush? fg)
    {
        // Tiny in-tree renderer — no third-party dep, theme-friendly,
        // cross-platform monospace font for code blocks (Markdown.Avalonia
        // crashes on Linux because it hardcodes Consolas).
        var rendered = MarkdownRenderer.Render(markdown ?? string.Empty);
        if (fg is not null && rendered is StackPanel sp)
        {
            // Apply the bubble's foreground colour to every text-bearing
            // child so prose stays readable; code blocks pull their own
            // styling from the theme.
            foreach (var child in sp.Children)
            {
                if (child is SelectableTextBlock stb) stb.Foreground = fg;
            }
        }
        return rendered;
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(text);
        SetStatus("Copied to clipboard.", isError: false);
    }

    private IBrush? TryBrush(string key)
        => this.TryFindResource(key, out var v) ? v as IBrush : null;

    private void ScrollToBottom()
    {
        // Background priority fires too early — the freshly-added bubble
        // hasn't been measured yet so ScrollToEnd targets the OLD end.
        // Loaded priority + an explicit UpdateLayout forces the measure
        // pass to complete before we ask the scroller for its new extent.
        Dispatcher.UIThread.Post(() =>
        {
            _historyScroll.UpdateLayout();
            _historyScroll.ScrollToEnd();
        }, DispatcherPriority.Loaded);
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

        history.Add(AiMessage.User(userContent));
        // Render the user's *typed* question rather than the script-laden
        // first-turn payload — the giant code dump is for the model, not
        // the visual transcript.
        RenderMessage(AiMessage.User(question));
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
        AiCompletionResult result;
        try
        {
            result = await _ai.SendAsync(conn, prompt.SystemMessage, history, _vault, _sendCts.Token);
        }
        catch (Exception ex)
        {
            result = AiCompletionResult.Fail(ex.Message);
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

        history.Add(AiMessage.Assistant(result.Text!));
        RenderMessage(AiMessage.Assistant(result.Text!));
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
        RenderHistory(Array.Empty<AiMessage>());
        SetStatus("Conversation cleared.", isError: false);
    }
}
