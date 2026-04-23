using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Avalonia-based <see cref="IPromptService"/>. Parents dialogs to the supplied owner window.
/// </summary>
public sealed class AvaloniaPromptService : IPromptService
{
    private readonly Window _owner;

    public AvaloniaPromptService(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public Task<string?> InputAsync(string title, string prompt, string? defaultValue = null)
        => new InputDialog(title, prompt, defaultValue).ShowDialog<string?>(_owner);

    public Task<bool> ConfirmAsync(string title, string message)
        => new ConfirmDialog(title, message).ShowDialog<bool>(_owner);

    public Task MessageAsync(string title, string message)
        => new MessageDialog(title, message).ShowDialog(_owner);
}
