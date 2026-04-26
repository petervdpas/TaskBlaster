using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

public enum AppMode { Scripts, Forms, Secrets, Connections }

public partial class ToolbarView : UserControl
{
    private readonly Button _runButton;
    private readonly Button _stopButton;
    private readonly Button _saveButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;
    private readonly ToggleButton _scriptsMode;
    private readonly ToggleButton _formsMode;
    private readonly ToggleButton _secretsMode;
    private readonly ToggleButton _connectionsMode;

    private bool _suppressModeEvent;

    public event EventHandler? RunClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? ConfigClicked;
    public event EventHandler? NewClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? RenameClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler<AppMode>? ModeChanged;

    public ToolbarView()
    {
        InitializeComponent();
        _runButton    = this.FindControl<Button>("RunButton")!;
        _stopButton   = this.FindControl<Button>("StopButton")!;
        _saveButton   = this.FindControl<Button>("SaveButton")!;
        _renameButton = this.FindControl<Button>("RenameButton")!;
        _deleteButton = this.FindControl<Button>("DeleteButton")!;
        _scriptsMode     = this.FindControl<ToggleButton>("ScriptsMode")!;
        _formsMode       = this.FindControl<ToggleButton>("FormsMode")!;
        _secretsMode     = this.FindControl<ToggleButton>("SecretsMode")!;
        _connectionsMode = this.FindControl<ToggleButton>("ConnectionsMode")!;
    }

    public bool CanRun    { get => _runButton.IsEnabled;    set => _runButton.IsEnabled    = value; }
    public bool CanStop   { get => _stopButton.IsEnabled;   set => _stopButton.IsEnabled   = value; }
    public bool CanSave   { get => _saveButton.IsEnabled;   set => _saveButton.IsEnabled   = value; }
    public bool CanModify { get => _renameButton.IsEnabled; set { _renameButton.IsEnabled = value; _deleteButton.IsEnabled = value; } }

    public AppMode Mode
    {
        get
        {
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
            _suppressModeEvent = false;
        }
    }

    public void SetRunLabel(string label) => _runButton.Content = label;

    private void OnScriptsModeClicked    (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Scripts);
    private void OnFormsModeClicked      (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Forms);
    private void OnSecretsModeClicked    (object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Secrets);
    private void OnConnectionsModeClicked(object? sender, RoutedEventArgs e) => SwitchTo(AppMode.Connections);

    private void SwitchTo(AppMode mode)
    {
        if (_suppressModeEvent) return;
        _suppressModeEvent = true;
        _scriptsMode.IsChecked     = mode == AppMode.Scripts;
        _formsMode.IsChecked       = mode == AppMode.Forms;
        _secretsMode.IsChecked     = mode == AppMode.Secrets;
        _connectionsMode.IsChecked = mode == AppMode.Connections;
        _suppressModeEvent = false;
        ModeChanged?.Invoke(this, mode);
    }

    private void OnRunClicked(object? sender, RoutedEventArgs e)    => RunClicked?.Invoke(this, EventArgs.Empty);
    private void OnStopClicked(object? sender, RoutedEventArgs e)   => StopClicked?.Invoke(this, EventArgs.Empty);
    private void OnConfigClicked(object? sender, RoutedEventArgs e) => ConfigClicked?.Invoke(this, EventArgs.Empty);
    private void OnNewClicked(object? sender, RoutedEventArgs e)    => NewClicked?.Invoke(this, EventArgs.Empty);
    private void OnSaveClicked(object? sender, RoutedEventArgs e)   => SaveClicked?.Invoke(this, EventArgs.Empty);
    private void OnRenameClicked(object? sender, RoutedEventArgs e) => RenameClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);
}
