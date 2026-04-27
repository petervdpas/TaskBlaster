namespace Acme.Domain;

/// <summary>
/// A customer in the canonical model. Identifier is opaque (server-issued).
/// </summary>
public sealed record Customer(
    string Id,
    string Name,
    string Email,
    CustomerTier Tier);

/// <summary>Subscription tier a customer is on.</summary>
public enum CustomerTier
{
    /// <summary>Free tier — limited features.</summary>
    Free,
    /// <summary>Paid individual tier.</summary>
    Pro,
    /// <summary>Organisation-wide contract with custom terms.</summary>
    Enterprise,
}
