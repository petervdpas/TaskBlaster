using System;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

public enum AppMode { Scripts, Forms, Secrets, Connections, Assistant }

public partial class ToolbarView : UserControl
{
    private readonly Border _actionsStrip;
    private readonly ContentPresenter _actionsContent;
    private readonly ToggleButton _scriptsMode;
    private readonly ToggleButton _formsMode;
    private readonly ToggleButton _secretsMode;
    private readonly ToggleButton _connectionsMode;
    private readonly ToggleButton _assistantMode;
    private readonly ToggleSwitch _terminalToggle;
    private readonly ToggleSwitch _chatToggle;
    private readonly TextBlock _chatToggleLabel;

    private bool _suppressModeEvent;
    private bool _suppressTerminalEvent;
    private bool _suppressChatEvent;

    public event EventHandler? ConfigClicked;
    public event EventHandler<AppMode>? ModeChanged;
    public event EventHandler<bool>? TerminalVisibilityChanged;
    public event EventHandler<bool>? ChatVisibilityChanged;

    public ToolbarView()
    {
        InitializeComponent();
        _actionsStrip    = this.FindControl<Border>("ActionsStrip")!;
        _actionsContent  = this.FindControl<ContentPresenter>("ActionsHost")!;
        _scriptsMode     = this.FindControl<ToggleButton>("ScriptsMode")!;
        _formsMode       = this.FindControl<ToggleButton>("FormsMode")!;
        _secretsMode     = this.FindControl<ToggleButton>("SecretsMode")!;
        _connectionsMode = this.FindControl<ToggleButton>("ConnectionsMode")!;
        _assistantMode    = this.FindControl<ToggleButton>("AssistantMode")!;
        _terminalToggle   = this.FindControl<ToggleSwitch>("TerminalToggle")!;
        _chatToggle       = this.FindControl<ToggleSwitch>("ChatToggle")!;
        _chatToggleLabel  = this.FindControl<TextBlock>("ChatToggleLabel")!;
    }

    /// <summary>Whether the script-scoped chat side panel is visible.</summary>
    public bool IsChatVisible
    {
        get => _chatToggle.IsChecked == true;
        set
        {
            _suppressChatEvent = true;
            _chatToggle.IsChecked = value;
            _suppressChatEvent = false;
        }
    }

    /// <summary>Hide the chat toggle entirely (for modes where it doesn't apply).</summary>
    public bool IsChatToggleVisible
    {
        get => _chatToggle.IsVisible;
        set
        {
            _chatToggle.IsVisible = value;
            _chatToggleLabel.IsVisible = value;
        }
    }

    private void OnChatToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressChatEvent) return;
        ChatVisibilityChanged?.Invoke(this, IsChatVisible);
    }

    /// <summary>
    /// Whether the Terminal panel toggle is on. Setting the value updates
    /// the switch without raising <see cref="TerminalVisibilityChanged"/>,
    /// so callers can sync the UI to a persisted value at startup.
    /// </summary>
    public bool IsTerminalVisible
    {
        get => _terminalToggle.IsChecked == true;
        set
        {
            _suppressTerminalEvent = true;
            _terminalToggle.IsChecked = value;
            _suppressTerminalEvent = false;
        }
    }

    /// <summary>
    /// Mode-specific content for the bottom (actions) strip. Each mode
    /// supplies its own panel of buttons; setting <c>null</c> hides the
    /// strip entirely.
    /// </summary>
    public Control? ActionsContent
    {
        get => _actionsContent.Content as Control;
        set
        {
            _actionsContent.Content = value;
            _actionsStrip.IsVisible = value is not null;
        }
    }

    public AppMode Mode
    {
        get
        {
            if (_assistantMode.IsChecked   == true) return AppMode.Assistant;
            if (_connectionsMode.IsChecked == true) return AppMode.Connections;
            if (_secretsMode.IsChecked     == true) return AppMode.Secrets;
            if (_formsMode.IsChecked       == true) return AppMode.Forms;
            return AppMode.Scripts;
        }
        set
        {
            _suppressModeEvent = true;
            _scriptsMode.IsChecked     = value == AppMode.Scripts;
            _formsMode.IsChecked       = value == AppMode.Forms;
            _secretsMode.IsChecked     = value == AppMode.Secrets;
            _connectionsMode.IsChecked = value == AppMode.Connections;
            _assistantMode.IsChecked   = value == AppMode.Assistant;
            _suppressModeEvent = false;
        }
    }

    private void OnScriptsModeClicked    (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Scripts);
    private void OnFormsModeClicked      (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Forms);
    private void OnSecretsModeClicked    (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Secrets);
    private void OnConnectionsModeClicked(object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Connections);
    private void OnAssistantModeClicked  (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Assistant);

    private void SwitchTo(AppMode mode)
    {
        if (_suppressModeEvent) return;
        _suppressModeEvent = true;
        _scriptsMode.IsChecked     = mode == AppMode.Scripts;
        _formsMode.IsChecked       = mode == AppMode.Forms;
        _secretsMode.IsChecked     = mode == AppMode.Secrets;
        _connectionsMode.IsChecked = mode == AppMode.Connections;
        _assistantMode.IsChecked   = mode == AppMode.Assistant;
        _suppressModeEvent = false;
        ModeChanged?.Invoke(this, mode);
    }

    private void OnConfigClicked(object? sender, RoutedEventArgs e) => ConfigClicked?.Invoke(this, EventArgs.Empty);

    private void OnTerminalToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressTerminalEvent) return;
        TerminalVisibilityChanged?.Invoke(this, IsTerminalVisible);
    }
}
