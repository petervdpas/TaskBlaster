namespace Acme.Domain;

/// <summary>
/// Postal address. Loose enough for international use:
/// <see cref="State"/> can be empty for countries without one,
/// and <see cref="Country"/> is a free-form string (typically ISO-3166
/// alpha-2 like "NL", "US") rather than a closed enum.
/// </summary>
public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    AddressKind Kind);

/// <summary>What an <see cref="Address"/> is used for on a person or order.</summary>
public enum AddressKind
{
    /// <summary>Where the person lives or the org is registered.</summary>
    Home,
    /// <summary>Workplace address.</summary>
    Work,
    /// <summary>Where deliveries should be sent.</summary>
    Shipping,
    /// <summary>Where invoices should be sent.</summary>
    Billing,
}
