using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TaskBlaster.Dialogs;

public enum PasswordDialogMode
{
    /// <summary>Single password field. Returns whatever was typed (including empty).</summary>
    Unlock,
    /// <summary>Two password fields that must match and be non-empty.</summary>
    Create,
}

public partial class PasswordDialog : Window
{
    private readonly TextBox _password;
    private readonly TextBox _confirm;
    private readonly StackPanel _confirmPanel;
    private readonly TextBlock _errorText;
    private readonly PasswordDialogMode _mode;

    public PasswordDialog() : this("Password", "Enter password.", PasswordDialogMode.Unlock) { }

    public PasswordDialog(string title, string prompt, PasswordDialogMode mode)
    {
        InitializeComponent();
        Title = title;
        this.FindControl<TextBlock>("PromptText")!.Text = prompt;
        _password     = this.FindControl<TextBox>("PasswordBox")!;
        _confirm      = this.FindControl<TextBox>("ConfirmBox")!;
        _confirmPanel = this.FindControl<StackPanel>("ConfirmPanel")!;
        _errorText    = this.FindControl<TextBlock>("ErrorText")!;
        _mode = mode;

        _confirmPanel.IsVisible = mode == PasswordDialogMode.Create;

        Opened += (_, _) => _password.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((string?)null); };
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var pw = _password.Text ?? string.Empty;

        if (_mode == PasswordDialogMode.Create)
        {
            if (string.IsNullOrEmpty(pw))
            {
                ShowError("Password cannot be empty.");
                return;
            }
            var confirm = _confirm.Text ?? string.Empty;
            if (pw != confirm)
            {
                ShowError("Passwords do not match.");
                return;
            }
        }

        Close(pw);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((string?)null);

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.IsVisible = true;
    }
}
