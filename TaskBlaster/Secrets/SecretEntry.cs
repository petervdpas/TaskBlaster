using System;

namespace TaskBlaster.Secrets;

/// <summary>
/// Decoded secret as seen by the UI: category/key/value plus the opaque
/// SecretBlast id under which the envelope is stored. The id is what we use
/// to update or delete the secret without caring about its current
/// category/key — users can rename freely.
/// </summary>
public sealed record SecretEntry(
    string Id,
    string Category,
    string Key,
    string Value,
    string? Description,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
