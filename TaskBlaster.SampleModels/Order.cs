using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
///     An order placed by a <see cref="Customer"/>. <see cref="Total"/> is
///     computed from <see cref="Lines"/> rather than stored, so the model
///     can't drift if a line is mutated through some other code path.
/// </summary>
public class Order
{
    /// <summary>Creates a new order.</summary>
    public Order(
        string id,
        string customerId,
        DateTimeOffset placedAtUtc,
        IReadOnlyList<OrderLine> lines,
        OrderStatus status)
    {
        Id = id;
        CustomerId = customerId;
        PlacedAtUtc = placedAtUtc;
        Lines = lines;
        Status = status;
    }

    /// <summary>Server-issued opaque identifier.</summary>
    public string Id { get; init; }

    /// <summary>The customer this order belongs to.</summary>
    public string CustomerId { get; init; }

    /// <summary>When the order was placed.</summary>
    public DateTimeOffset PlacedAtUtc { get; init; }

    /// <summary>The line items on this order.</summary>
    public IReadOnlyList<OrderLine> Lines { get; init; }

    /// <summary>Lifecycle state.</summary>
    public OrderStatus Status { get; init; }

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
public class OrderLine
{
    /// <summary>Creates a new order line.</summary>
    public OrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>The product SKU.</summary>
    public string Sku { get; init; }

    /// <summary>Number of units.</summary>
    public int Quantity { get; init; }

    /// <summary>Per-unit price at the time of order.</summary>
    public decimal UnitPrice { get; init; }

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
