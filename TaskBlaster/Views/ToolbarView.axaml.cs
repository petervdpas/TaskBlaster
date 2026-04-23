using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

public partial class ToolbarView : UserControl
{
    private readonly Button _runButton;
    private readonly Button _stopButton;
    private readonly Button _saveButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;

    public event EventHandler? RunClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? ThemeClicked;
    public event EventHandler? NewClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? RenameClicked;
    public event EventHandler? DeleteClicked;

    public ToolbarView()
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

    private void OnRunClicked(object? sender, RoutedEventArgs e)    => RunClicked?.Invoke(this, EventArgs.Empty);
    private void OnStopClicked(object? sender, RoutedEventArgs e)   => StopClicked?.Invoke(this, EventArgs.Empty);
    private void OnThemeClicked(object? sender, RoutedEventArgs e)  => ThemeClicked?.Invoke(this, EventArgs.Empty);
    private void OnNewClicked(object? sender, RoutedEventArgs e)    => NewClicked?.Invoke(this, EventArgs.Empty);
    private void OnSaveClicked(object? sender, RoutedEventArgs e)   => SaveClicked?.Invoke(this, EventArgs.Empty);
    private void OnRenameClicked(object? sender, RoutedEventArgs e) => RenameClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);
}
