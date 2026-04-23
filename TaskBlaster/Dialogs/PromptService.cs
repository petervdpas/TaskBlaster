using System.Threading.Tasks;
using Avalonia.Controls;

namespace TaskBlaster.Dialogs;

public static class PromptService
{
    public static Task<string?> InputAsync(Window owner, string title, string prompt, string? defaultValue = null)
        => new InputDialog(title, prompt, defaultValue).ShowDialog<string?>(owner);

    public static Task<bool> ConfirmAsync(Window owner, string title, string message)
        => new ConfirmDialog(title, message).ShowDialog<bool>(owner);

    public static Task MessageAsync(Window owner, string title, string message)
        => new MessageDialog(title, message).ShowDialog(owner);
}
