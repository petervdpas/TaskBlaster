using Avalonia.Controls;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Dialogs;

public sealed class AvaloniaPromptServiceFactory : IPromptServiceFactory
{
    public IPromptService Create(Window owner) => new AvaloniaPromptService(owner);
}
