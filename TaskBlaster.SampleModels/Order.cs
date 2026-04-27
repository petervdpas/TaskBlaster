using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
/// An order placed by a <see cref="Customer"/>. <see cref="Total"/> is
/// computed from <see cref="Lines"/> rather than stored, so the model
/// can't drift if a line is mutated through some other code path.
/// </summary>
public sealed record Order(
    string Id,
    string CustomerId,
    DateTimeOffset PlacedAtUtc,
    IReadOnlyList<OrderLine> Lines,
    OrderStatus Status)
{
    /// <summary>Sum of <see cref="OrderLine.LineTotal"/> across all lines.</summary>
    public decimal Total
    {
        get
        {
            decimal sum = 0m;
            foreach (var l in Lines) sum += l.LineTotal;
            return sum;
        }
    }
}

/// <summary>A single line item on an <see cref="Order"/>.</summary>
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice)
{
    /// <summary><see cref="Quantity"/> times <see cref="UnitPrice"/>.</summary>
    public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>Lifecycle state of an <see cref="Order"/>.</summary>
public enum OrderStatus
{
    /// <summary>Created but not yet paid.</summary>
    Pending,
    /// <summary>Payment captured, awaiting fulfilment.</summary>
    Paid,
    /// <summary>Handed off to the carrier.</summary>
    Shipped,
    /// <summary>Carrier confirmed delivery.</summary>
    Delivered,
    /// <summary>Cancelled by either party before fulfilment.</summary>
    Cancelled,
}
