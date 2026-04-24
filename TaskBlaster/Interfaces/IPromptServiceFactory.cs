using Avalonia.Controls;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Builds an <see cref="IPromptService"/> bound to a specific owner window.
/// Lets us register prompts in the DI container without creating a cycle
/// between <see cref="IPromptService"/> and the window that owns its dialogs.
/// </summary>
public interface IPromptServiceFactory
{
    IPromptService Create(Window owner);
}
