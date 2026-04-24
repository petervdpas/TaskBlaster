using System.Collections.Generic;
using System.Threading.Tasks;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// Test fake for <see cref="IPromptService"/>. Queues canned responses and
/// records the prompts that were shown.
/// </summary>
public sealed class FakePromptService : IPromptService
{
    public record InputCall(string Title, string Prompt, string? Default);
    public record ConfirmCall(string Title, string Message);
    public record MessageCall(string Title, string Message);
    public record PasswordCall(string Title, string Prompt, bool Confirm);

    public Queue<string?> InputResponses    { get; } = new();
    public Queue<bool>    ConfirmResponses  { get; } = new();
    public Queue<string?> PasswordResponses { get; } = new();

    public List<InputCall>    InputCalls    { get; } = new();
    public List<ConfirmCall>  ConfirmCalls  { get; } = new();
    public List<MessageCall>  MessageCalls  { get; } = new();
    public List<PasswordCall> PasswordCalls { get; } = new();

    public Task<string?> InputAsync(string title, string prompt, string? defaultValue = null)
    {
        InputCalls.Add(new InputCall(title, prompt, defaultValue));
        var response = InputResponses.Count > 0 ? InputResponses.Dequeue() : null;
        return Task.FromResult(response);
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        ConfirmCalls.Add(new ConfirmCall(title, message));
        var response = ConfirmResponses.Count > 0 ? ConfirmResponses.Dequeue() : false;
        return Task.FromResult(response);
    }

    public Task MessageAsync(string title, string message)
    {
        MessageCalls.Add(new MessageCall(title, message));
        return Task.CompletedTask;
    }

    public Task<string?> PasswordAsync(string title, string prompt, bool confirm = false)
    {
        PasswordCalls.Add(new PasswordCall(title, prompt, confirm));
        var response = PasswordResponses.Count > 0 ? PasswordResponses.Dequeue() : null;
        return Task.FromResult(response);
    }
}
