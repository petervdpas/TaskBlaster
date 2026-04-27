using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
/// Hand-built fixture data so a smoke-test script can do something
/// meaningful without needing a backing store.
/// </summary>
public static class SampleData
{
    /// <summary>Three customers across the three tiers.</summary>
    public static IReadOnlyList<Customer> Customers { get; } = new[]
    {
        new Customer("C-1", "Ada Lovelace",   "ada@example.com",   CustomerTier.Enterprise),
        new Customer("C-2", "Alan Turing",    "alan@example.com",  CustomerTier.Pro),
        new Customer("C-3", "Grace Hopper",   "grace@example.com", CustomerTier.Free),
    };

    /// <summary>A short catalog spanning out-of-stock, expensive, and cheap items.</summary>
    public static IReadOnlyList<Product> Products { get; } = new[]
    {
        new Product("SKU-AAA", "Analytical Engine", 9999m, InStock: false),
        new Product("SKU-BBB", "Bombe Replica",     4999m, InStock: true),
        new Product("SKU-CCC", "COBOL Sticker",        2m, InStock: true),
    };

    /// <summary>A single multi-line order so a script can demo <see cref="Order.Total"/>.</summary>
    public static IReadOnlyList<Order> Orders { get; } = new[]
    {
        new Order(
            Id:          "O-1",
            CustomerId:  "C-1",
            PlacedAtUtc: new DateTimeOffset(2026, 1, 14, 10, 30, 0, TimeSpan.Zero),
            Lines: new[]
            {
                new OrderLine("SKU-BBB", 1,  4999m),
                new OrderLine("SKU-CCC", 4,     2m),
            },
            Status: OrderStatus.Paid),
    };

    /// <summary>
    /// Reusable address fixtures. Keyed by id rather than embedded in the
    /// people list so multiple <see cref="Person"/> records can share the
    /// same address object — the typical "head office" pattern.
    /// </summary>
    public static IReadOnlyDictionary<string, Address> Addresses { get; } = new Dictionary<string, Address>
    {
        ["A-LON"] = new Address("23 Hanover Square", null,            "London",     "",   "W1S 1JB", "GB", AddressKind.Home),
        ["A-NYC"] = new Address("350 5th Ave",       "Floor 21",      "New York",   "NY", "10118",   "US", AddressKind.Work),
        ["A-AMS"] = new Address("Damrak 70",         null,            "Amsterdam",  "",   "1012 LM", "NL", AddressKind.Shipping),
    };

    /// <summary>People underlying the three sample customers, with channels and addresses.</summary>
    public static IReadOnlyList<Person> People { get; } = new[]
    {
        new Person(
            Id:          "P-1",
            GivenName:   "Ada",
            FamilyName:  "Lovelace",
            DateOfBirth: new DateOnly(1815, 12, 10),
            Channels: new[]
            {
                new ContactChannel(ContactChannelKind.Email, "ada@example.com",   IsPrimary: true),
                new ContactChannel(ContactChannelKind.Phone, "+441234567890",     IsPrimary: false),
            },
            Addresses: new[] { Addresses["A-LON"], Addresses["A-NYC"] }),

        new Person(
            Id:          "P-2",
            GivenName:   "Alan",
            FamilyName:  "Turing",
            DateOfBirth: new DateOnly(1912, 6, 23),
            Channels: new[] { new ContactChannel(ContactChannelKind.Email, "alan@example.com", IsPrimary: true) },
            Addresses: new[] { Addresses["A-LON"] }),

        new Person(
            Id:          "P-3",
            GivenName:   "Grace",
            FamilyName:  "Hopper",
            DateOfBirth: new DateOnly(1906, 12, 9),
            Channels: new[]
            {
                new ContactChannel(ContactChannelKind.Email, "grace@example.com", IsPrimary: true),
                new ContactChannel(ContactChannelKind.Url,   "https://example.com/grace", IsPrimary: false),
            },
            Addresses: new[] { Addresses["A-NYC"] }),
    };
}
