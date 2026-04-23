using System.Threading.Tasks;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Shows modal prompts (input, confirm, message) to the user.
/// Avalonia implementation parents dialogs to the main window; test fakes
/// can return canned responses.
/// </summary>
public interface IPromptService
{
    Task<string?> InputAsync(string title, string prompt, string? defaultValue = null);
    Task<bool>    ConfirmAsync(string title, string message);
    Task          MessageAsync(string title, string message);
}
