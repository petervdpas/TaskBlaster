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
    private readonly Button _reveal;
    private readonly PasswordDialogMode _mode;
    private bool _revealed;

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
        _reveal       = this.FindControl<Button>("RevealButton")!;
        _mode = mode;

        _confirmPanel.IsVisible = mode == PasswordDialogMode.Create;

        // Clear any stale error the moment the user edits either field so
        // the UI doesn't lie about the current state.
        _password.TextChanged += (_, _) => ClearError();
        _confirm.TextChanged  += (_, _) => ClearError();

        Opened += (_, _) => _password.Focus();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((string?)null); };
    }

    private void OnToggleReveal(object? sender, RoutedEventArgs e)
    {
        _revealed = !_revealed;
        _password.PasswordChar = _revealed ? default : '•';
        _confirm.PasswordChar  = _revealed ? default : '•';
        _reveal.Content = _revealed ? "🙈" : "👁";
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

    private void ClearError()
    {
        if (!_errorText.IsVisible) return;
        _errorText.IsVisible = false;
    }
}
