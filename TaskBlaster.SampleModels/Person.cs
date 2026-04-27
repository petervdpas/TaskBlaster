using System;
using System.Collections.Generic;

namespace Acme.Domain;

/// <summary>
/// A natural person. <see cref="Customer"/> is the commercial role
/// projection; this is the underlying human, with optional contact
/// channels and addresses. Stable identifier is server-issued.
/// </summary>
public sealed record Person(
    string Id,
    string GivenName,
    string FamilyName,
    DateOnly? DateOfBirth,
    IReadOnlyList<ContactChannel> Channels,
    IReadOnlyList<Address> Addresses)
{
    /// <summary>Convenience accessor: <c>"GivenName FamilyName"</c>.</summary>
    public string FullName => $"{GivenName} {FamilyName}".Trim();
}

/// <summary>
/// One way to reach a <see cref="Person"/>. Kind is the channel type;
/// <see cref="Value"/> is the channel-shaped string (an email address, a
/// phone number, etc.).
/// </summary>
public sealed record ContactChannel(ContactChannelKind Kind, string Value, bool IsPrimary);

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
