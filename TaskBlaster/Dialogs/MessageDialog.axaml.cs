using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TaskBlaster.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog() : this("Message", "") { }

    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter) Close();
        };
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
