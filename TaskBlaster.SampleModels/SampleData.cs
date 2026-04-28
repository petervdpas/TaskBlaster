using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
///     Hand-built fixture data so a smoke-test script can do something
///     meaningful without needing a backing store.
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
        new Product("SKU-AAA", "Analytical Engine", 9999m, false),
        new Product("SKU-BBB", "Bombe Replica",     4999m, true),
        new Product("SKU-CCC", "COBOL Sticker",        2m, true),
    };

    /// <summary>A single multi-line order so a script can demo <see cref="Order.Total"/>.</summary>
    public static IReadOnlyList<Order> Orders { get; } = new[]
    {
        new Order(
            id:          "O-1",
            customerId:  "C-1",
            placedAtUtc: new DateTimeOffset(2026, 1, 14, 10, 30, 0, TimeSpan.Zero),
            lines: new[]
            {
                new OrderLine("SKU-BBB", 1,  4999m),
                new OrderLine("SKU-CCC", 4,     2m),
            },
            status: OrderStatus.Paid),
    };

    /// <summary>
    ///     Reusable address fixtures. Keyed by id rather than embedded in the
    ///     people list so multiple <see cref="Person"/> records can share the
    ///     same address object — the typical "head office" pattern.
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
            id:          "P-1",
            givenName:   "Ada",
            familyName:  "Lovelace",
            dateOfBirth: new DateOnly(1815, 12, 10),
            channels: new[]
            {
                new ContactChannel(ContactChannelKind.Email, "ada@example.com",   true),
                new ContactChannel(ContactChannelKind.Phone, "+441234567890",     false),
            },
            addresses: new[] { Addresses["A-LON"], Addresses["A-NYC"] }),

        new Person(
            id:          "P-2",
            givenName:   "Alan",
            familyName:  "Turing",
            dateOfBirth: new DateOnly(1912, 6, 23),
            channels: new[] { new ContactChannel(ContactChannelKind.Email, "alan@example.com", true) },
            addresses: new[] { Addresses["A-LON"] }),

        new Person(
            id:          "P-3",
            givenName:   "Grace",
            familyName:  "Hopper",
            dateOfBirth: new DateOnly(1906, 12, 9),
            channels: new[]
            {
                new ContactChannel(ContactChannelKind.Email, "grace@example.com", true),
                new ContactChannel(ContactChannelKind.Url,   "https://example.com/grace", false),
            },
            addresses: new[] { Addresses["A-NYC"] }),
    };
}
