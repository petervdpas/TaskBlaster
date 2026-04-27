using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

/// <summary>
/// Action strip for Assistant mode. Hosted in
/// <see cref="ToolbarView.ActionsContent"/>. Owned and wired by
/// <see cref="AssistantView"/>.
/// </summary>
public partial class AssistantActionsView : UserControl
{
    private readonly Button _saveButton;
    private readonly Button _deleteButton;

    public event EventHandler? AddClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? DeleteClicked;

    public AssistantActionsView()
    {
        InitializeComponent();
        _saveButton   = this.FindControl<Button>("SaveButton")!;
        _deleteButton = this.FindControl<Button>("DeleteButton")!;
    }

    /// <summary>Whether a block is currently selected (controls Delete).</summary>
    public bool HasSelection
    {
        get => _deleteButton.IsEnabled;
        set => _deleteButton.IsEnabled = value;
    }

    /// <summary>Whether the editor has unsaved changes (controls Save).</summary>
    public bool CanSave
    {
        get => _saveButton.IsEnabled;
        set => _saveButton.IsEnabled = value;
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e)    => AddClicked?.Invoke(this, EventArgs.Empty);
    private void OnSaveClicked(object? sender, RoutedEventArgs e)   => SaveClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);
}
