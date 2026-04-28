namespace Acme.Domain;

/// <summary>
///     Postal address. Loose enough for international use:
///     <see cref="State"/> can be empty for countries without one,
///     and <see cref="Country"/> is a free-form string (typically ISO-3166
///     alpha-2 like "NL", "US") rather than a closed enum.
/// </summary>
public class Address
{
    /// <summary>Creates a new address.</summary>
    public Address(
        string line1,
        string? line2,
        string city,
        string state,
        string postalCode,
        string country,
        AddressKind kind)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
        Kind = kind;
    }

    /// <summary>First address line (street + number).</summary>
    public string Line1 { get; init; }

    /// <summary>Optional second address line (suite, floor, ...).</summary>
    public string? Line2 { get; init; }

    /// <summary>City or locality.</summary>
    public string City { get; init; }

    /// <summary>State, province, or region. Empty when not applicable.</summary>
    public string State { get; init; }

    /// <summary>Postal / ZIP code.</summary>
    public string PostalCode { get; init; }

    /// <summary>Country, typically ISO-3166 alpha-2.</summary>
    public string Country { get; init; }

    /// <summary>What this address is used for.</summary>
    public AddressKind Kind { get; init; }
}

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
