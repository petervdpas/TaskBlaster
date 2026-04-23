using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TaskBlaster.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() : this("Confirm", "") { }

    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(false);
        };
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);
    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
