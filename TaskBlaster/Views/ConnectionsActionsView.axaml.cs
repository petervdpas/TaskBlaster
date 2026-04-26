using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

/// <summary>
/// Action strip for Connections mode. Hosted in
/// <see cref="ToolbarView.ActionsContent"/>. Owned and wired by
/// <see cref="ConnectionsView"/>.
/// </summary>
public partial class ConnectionsActionsView : UserControl
{
    private readonly Button _deleteButton;
    private readonly Button _addFieldButton;

    public event EventHandler? AddClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? AddFieldClicked;

    public ConnectionsActionsView()
    {
        InitializeComponent();
        _deleteButton   = this.FindControl<Button>("DeleteButton")!;
        _addFieldButton = this.FindControl<Button>("AddFieldButton")!;
    }

    public bool HasSelection
    {
        get => _deleteButton.IsEnabled;
        set
        {
            _deleteButton.IsEnabled = value;
            _addFieldButton.IsEnabled = value;
        }
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e)      => AddClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e)   => DeleteClicked?.Invoke(this, EventArgs.Empty);
    private void OnAddFieldClicked(object? sender, RoutedEventArgs e) => AddFieldClicked?.Invoke(this, EventArgs.Empty);
}
