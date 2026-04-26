using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

/// <summary>
/// Action strip for Secrets mode. Hosted in
/// <see cref="ToolbarView.ActionsContent"/>. Owned and wired by
/// <see cref="SecretsView"/>: this control is just a typed event surface
/// so all vault/state logic stays in the parent view.
/// </summary>
public partial class SecretsActionsView : UserControl
{
    private readonly StackPanel _lockedActions;
    private readonly StackPanel _unlockedActions;
    private readonly Button _unlockButton;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Button _copyButton;

    public event EventHandler? UnlockClicked;
    public event EventHandler? AddClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? CopyClicked;
    public event EventHandler? LockClicked;
    public event EventHandler? CategoriesClicked;
    public event EventHandler? ChangePasswordClicked;
    public event EventHandler? DestroyClicked;

    public SecretsActionsView()
    {
        InitializeComponent();
        _lockedActions   = this.FindControl<StackPanel>("LockedActions")!;
        _unlockedActions = this.FindControl<StackPanel>("UnlockedActions")!;
        _unlockButton    = this.FindControl<Button>("UnlockButton")!;
        _editButton      = this.FindControl<Button>("EditButton")!;
        _deleteButton    = this.FindControl<Button>("DeleteButton")!;
        _copyButton      = this.FindControl<Button>("CopyButton")!;
    }

    public bool IsUnlocked
    {
        get => _unlockedActions.IsVisible;
        set
        {
            _unlockedActions.IsVisible = value;
            _lockedActions.IsVisible = !value;
        }
    }

    public bool HasSelection
    {
        get => _editButton.IsEnabled;
        set
        {
            _editButton.IsEnabled = value;
            _deleteButton.IsEnabled = value;
            _copyButton.IsEnabled = value;
        }
    }

    public bool CanUnlock { get => _unlockButton.IsEnabled; set => _unlockButton.IsEnabled = value; }

    private void OnUnlockClicked(object? sender, RoutedEventArgs e)         => UnlockClicked?.Invoke(this, EventArgs.Empty);
    private void OnAddClicked(object? sender, RoutedEventArgs e)            => AddClicked?.Invoke(this, EventArgs.Empty);
    private void OnEditClicked(object? sender, RoutedEventArgs e)           => EditClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, RoutedEventArgs e)         => DeleteClicked?.Invoke(this, EventArgs.Empty);
    private void OnCopyClicked(object? sender, RoutedEventArgs e)           => CopyClicked?.Invoke(this, EventArgs.Empty);
    private void OnLockClicked(object? sender, RoutedEventArgs e)           => LockClicked?.Invoke(this, EventArgs.Empty);
    private void OnCategoriesClicked(object? sender, RoutedEventArgs e)     => CategoriesClicked?.Invoke(this, EventArgs.Empty);
    private void OnChangePasswordClicked(object? sender, RoutedEventArgs e) => ChangePasswordClicked?.Invoke(this, EventArgs.Empty);
    private void OnDestroyClicked(object? sender, RoutedEventArgs e)        => DestroyClicked?.Invoke(this, EventArgs.Empty);
}
