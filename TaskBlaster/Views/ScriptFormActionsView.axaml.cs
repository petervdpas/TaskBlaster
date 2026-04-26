using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

/// <summary>
/// Action strip for Scripts and Forms modes. Hosted in
/// <see cref="ToolbarView.ActionsContent"/>. State (Can*) is owned here
/// so MainWindow can flip enable flags as the script run state, dirty
/// flag, and selected file change.
/// </summary>
public partial class ScriptFormActionsView : UserControl
{
    private readonly Button _runButton;
    private readonly Button _stopButton;
    private readonly Button _saveButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;

    public event EventHandler? RunClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? NewClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? RenameClicked;
    public event EventHandler? DeleteClicked;

    public ScriptFormActionsView()
    {
        InitializeComponent();
        _runButton    = this.FindControl<Button>("RunButton")!;
        _stopButton   = this.FindControl<Button>("StopButton")!;
        _saveButton   = this.FindControl<Button>("SaveButton")!;
        _renameButton = this.FindControl<Button>("RenameButton")!;
        _deleteButton = this.FindControl<Button>("DeleteButton")!;
    }

    public bool CanRun    { get => _runButton.IsEnabled;    set => _runButton.IsEnabled    = value; }
    public bool CanStop   { get => _stopButton.IsEnabled;   set => _stopButton.IsEnabled   = value; }
    public bool CanSave   { get => _saveButton.IsEnabled;   set => _saveButton.IsEnabled   = value; }
    public bool CanModify { get => _renameButton.IsEnabled; set { _renameButton.IsEnabled = value; _deleteButton.IsEnabled = value; } }

    public void SetRunLabel(string label) => _runButton.Content = label;

    private void OnRunClicked(object? sender, RoutedEventArgs e)    => RunClicked?.Invoke(this, EventArgs.Empty);
    private void OnStopClicked(object? sender, RoutedEventArgs e)   => StopClicked?.Invoke(this, EventArgs.Empty);
    private void OnNewClicked(object? sender, RoutedEventArgs e)    => NewClicked?.Invoke(this, EventArgs.Empty);
    private void OnSaveClicked(object? sender, RoutedEventArgs e)   => SaveClicked?.Invoke(this, EventArgs.Empty);
    private void OnRenameClicked(object? sender, RoutedEventArgs e) => RenameClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);
}
