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

    public FormSettingsDialog() : this(null) { }

    public FormSettingsDialog(IFormDocument? document)
    {
        InitializeComponent();
        _actions    = this.FindControl<ActionsEditorView>("ActionsEditor")!;
        _visibility = this.FindControl<VisibilityEditorView>("VisibilityEditor")!;
        _actions.Document    = document;
        _visibility.Document = document;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
