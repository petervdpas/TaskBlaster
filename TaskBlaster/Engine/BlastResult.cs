namespace TaskBlaster.Engine;

public enum BlastStatus { Ok, Error, Cancelled }

public sealed record BlastResult(BlastStatus Status, string? Message, string? Details = null)
{
    public static BlastResult Ok() => new(BlastStatus.Ok, null);
    public static BlastResult Error(string message, string? details = null) => new(BlastStatus.Error, message, details);
    public static BlastResult Cancelled(string? message = null) => new(BlastStatus.Cancelled, message);
}
