namespace Acme.Domain;

/// <summary>
///     A product in the catalog. <see cref="Sku"/> is the stable identifier
///     referenced from <see cref="OrderLine.Sku"/>.
/// </summary>
public class Product
{
    /// <summary>Creates a new product.</summary>
    public Product(string sku, string name, decimal unitPrice, bool inStock)
    {
        Sku = sku;
        Name = name;
        UnitPrice = unitPrice;
        InStock = inStock;
    }

    /// <summary>Stock-keeping unit. Stable across price/name changes.</summary>
    public string Sku { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; }

    /// <summary>Current unit price.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Whether the product is currently in stock.</summary>
    public bool InStock { get; init; }
}
