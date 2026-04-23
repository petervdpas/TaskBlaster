using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TaskBlaster.Dialogs;

public partial class InputDialog : Window
{
    private readonly TextBox _input;

    public InputDialog() : this("Input", "", null) { }

    public InputDialog(string title, string prompt, string? defaultValue)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("PromptText")!.Text = prompt;
        _input = this.FindControl<TextBox>("InputBox")!;
        _input.Text = defaultValue ?? string.Empty;

        Opened += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(null);
        };
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(_input.Text ?? string.Empty);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
