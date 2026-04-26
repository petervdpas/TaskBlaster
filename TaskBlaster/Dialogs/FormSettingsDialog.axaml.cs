using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster.Dialogs;

public partial class FormSettingsDialog : Window
{
    private readonly ActionsEditorView _actions;
    private readonly VisibilityEditorView _visibility;
    private readonly SizeEditorView _size;
    private readonly TextBlock _statusText;

    private readonly IFormDocument? _document;

    public FormSettingsDialog() : this(null) { }

    public FormSettingsDialog(IFormDocument? document)
    {
        InitializeComponent();
        _actions    = this.FindControl<ActionsEditorView>("ActionsEditor")!;
        _visibility = this.FindControl<VisibilityEditorView>("VisibilityEditor")!;
        _size       = this.FindControl<SizeEditorView>("SizeEditor")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;

        _document = document;
        _actions.Document    = document;
        _visibility.Document = document;
        _size.Document       = document;

        UpdateStatus();
        if (_document is not null)
            _document.DirtyChanged += OnDirtyChanged;
        Closed += (_, _) =>
        {
            if (_document is not null)
                _document.DirtyChanged -= OnDirtyChanged;
        };

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnDirtyChanged(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        if (_document is null) { _statusText.Text = string.Empty; return; }
        _statusText.Text = _document.IsDirty ? "● Unsaved changes" : "✓ Saved";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
