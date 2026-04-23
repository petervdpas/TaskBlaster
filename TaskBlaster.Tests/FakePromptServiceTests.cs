using System.Threading.Tasks;

namespace TaskBlaster.Tests;

public class FakePromptServiceTests
{
    [Fact]
    public async Task InputAsync_ReturnsQueuedResponse_AndRecordsCall()
    {
        var fake = new FakePromptService();
        fake.InputResponses.Enqueue("alice");

        var result = await fake.InputAsync("Hello", "Your name?", defaultValue: "world");

        Assert.Equal("alice", result);
        Assert.Single(fake.InputCalls);
        Assert.Equal("Hello",      fake.InputCalls[0].Title);
        Assert.Equal("Your name?", fake.InputCalls[0].Prompt);
        Assert.Equal("world",      fake.InputCalls[0].Default);
    }

    [Fact]
    public async Task InputAsync_NoQueuedResponse_DefaultsToNull()
    {
        var fake = new FakePromptService();

        var result = await fake.InputAsync("T", "P");

        Assert.Null(result);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsQueuedBool()
    {
        var fake = new FakePromptService();
        fake.ConfirmResponses.Enqueue(true);
        fake.ConfirmResponses.Enqueue(false);

        Assert.True(await fake.ConfirmAsync("T", "Yes?"));
        Assert.False(await fake.ConfirmAsync("T", "Yes?"));
        Assert.Equal(2, fake.ConfirmCalls.Count);
    }

    [Fact]
    public async Task MessageAsync_JustRecordsCall()
    {
        var fake = new FakePromptService();
        await fake.MessageAsync("Info", "Done");

        Assert.Single(fake.MessageCalls);
        Assert.Equal("Info", fake.MessageCalls[0].Title);
        Assert.Equal("Done", fake.MessageCalls[0].Message);
    }
}
