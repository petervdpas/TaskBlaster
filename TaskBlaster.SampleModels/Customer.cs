namespace Acme.Domain;

/// <summary>
///     A customer in the canonical model. Identifier is opaque (server-issued).
/// </summary>
public class Customer
{
    /// <summary>Creates a new customer.</summary>
    public Customer(string id, string name, string email, CustomerTier tier)
    {
        Id = id;
        Name = name;
        Email = email;
        Tier = tier;
    }

    /// <summary>Server-issued opaque identifier.</summary>
    public string Id { get; init; }

    /// <summary>Human-readable customer name.</summary>
    public string Name { get; init; }

    /// <summary>Primary email address on record.</summary>
    public string Email { get; init; }

    /// <summary>Subscription tier.</summary>
    public CustomerTier Tier { get; init; }
}

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
