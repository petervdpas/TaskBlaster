namespace TaskBlaster.Engine;

public enum BlastStatus { Ok, Error, Cancelled }

public sealed record BlastResult(BlastStatus Status, string? Message)
{
    public static BlastResult Ok() => new(BlastStatus.Ok, null);
    public static BlastResult Error(string message) => new(BlastStatus.Error, message);
    public static BlastResult Cancelled() => new(BlastStatus.Cancelled, null);
}
