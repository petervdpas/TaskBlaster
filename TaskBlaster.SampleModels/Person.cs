using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
///     A natural person. <see cref="Customer"/> is the commercial role
///     projection; this is the underlying human, with optional contact
///     channels and addresses. Stable identifier is server-issued.
/// </summary>
public class Person
{
    /// <summary>Creates a new person record.</summary>
    public Person(
        string id,
        string givenName,
        string familyName,
        DateOnly? dateOfBirth,
        IReadOnlyList<ContactChannel> channels,
        IReadOnlyList<Address> addresses)
    {
        Id = id;
        GivenName = givenName;
        FamilyName = familyName;
        DateOfBirth = dateOfBirth;
        Channels = channels;
        Addresses = addresses;
    }

    /// <summary>Server-issued opaque identifier.</summary>
    public string Id { get; init; }

    /// <summary>Given (first) name.</summary>
    public string GivenName { get; init; }

    /// <summary>Family (last) name.</summary>
    public string FamilyName { get; init; }

    /// <summary>Optional date of birth.</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Ways to reach this person.</summary>
    public IReadOnlyList<ContactChannel> Channels { get; init; }

    /// <summary>Postal addresses on record.</summary>
    public IReadOnlyList<Address> Addresses { get; init; }

    /// <summary>Convenience accessor: <c>"GivenName FamilyName"</c>.</summary>
    public string FullName => $"{GivenName} {FamilyName}".Trim();
}

/// <summary>
///     One way to reach a <see cref="Person"/>. Kind is the channel type;
///     <see cref="Value"/> is the channel-shaped string (an email address, a
///     phone number, etc.).
/// </summary>
public class ContactChannel
{
    /// <summary>Creates a new contact channel.</summary>
    public ContactChannel(ContactChannelKind kind, string value, bool isPrimary)
    {
        Kind = kind;
        Value = value;
        IsPrimary = isPrimary;
    }

    /// <summary>What this channel represents.</summary>
    public ContactChannelKind Kind { get; init; }

    /// <summary>The channel-shaped string.</summary>
    public string Value { get; init; }

    /// <summary>Whether this is the person's preferred channel of this kind.</summary>
    public bool IsPrimary { get; init; }
}

/// <summary>What a <see cref="ContactChannel"/> represents.</summary>
public enum ContactChannelKind
{
    /// <summary>Email address.</summary>
    Email,
    /// <summary>E.164-formatted phone number.</summary>
    Phone,
    /// <summary>Generic URL — homepage, social profile, etc.</summary>
    Url,
}
