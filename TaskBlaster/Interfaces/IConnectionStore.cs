using System.Collections.Generic;
using TaskBlaster.Connections;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Reads and writes the named-connection config (default
/// <c>~/.taskblaster/connections.json</c>). Used at script run time by
/// the connections-aware resolver, and at edit time by the (yet to be
/// built) Connections UI.
/// </summary>
public interface IConnectionStore
{
    /// <summary>All known connections, name-sorted.</summary>
    IReadOnlyList<Connection> List();

    /// <summary>Look up a connection by name (case-insensitive). Null if absent.</summary>
    Connection? Get(string name);

    /// <summary>Persist (or overwrite) one connection.</summary>
    void Save(Connection connection);

    /// <summary>Remove a connection. No-op if not present.</summary>
    void Remove(string name);

    /// <summary>Re-read the backing file. Discards any in-memory changes that were not persisted.</summary>
    void Reload();
}
