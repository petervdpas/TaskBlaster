namespace Acme.Domain;

/// <summary>
/// A product in the catalog. <see cref="Sku"/> is the stable identifier
/// referenced from <see cref="OrderLine.Sku"/>.
/// </summary>
public sealed record Product(
    string Sku,
    string Name,
    decimal UnitPrice,
    bool InStock);
