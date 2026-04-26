using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Connections;

/// <summary>
/// JSON-backed <see cref="IConnectionStore"/>. The on-disk shape is a
/// dict of dict-of-fields:
/// <code>
/// {
///   "github": {
///     "baseUrl":  { "value":    "https://api.github.com" },
///     "token":    { "fromVault": { "category": "github-secrets", "key": "pat" } }
///   }
/// }
/// </code>
/// The connection name is the outer dict key; <see cref="Connection.Name"/>
/// mirrors it in memory but is not persisted as a property.
/// Malformed field entries (neither <c>value</c> nor <c>fromVault</c> set)
/// are silently dropped on load so a partially hand-edited file still
/// parses.
/// </summary>
public sealed class ConnectionStore : IConnectionStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, Connection> _byName = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Reload();
    }

    public IReadOnlyList<Connection> List() =>
        _byName.Values
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public Connection? Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _byName.TryGetValue(name, out var c) ? c : null;
    }

    public void Save(Connection connection)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(connection.Name))
            throw new ArgumentException("Connection name is required.", nameof(connection));

        _byName[connection.Name] = connection;
        Persist();
    }

    public void Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_byName.Remove(name)) Persist();
    }

    public void Reload()
    {
        _byName.Clear();
        if (!File.Exists(_filePath)) return;

        Dictionary<string, Dictionary<string, FieldDto>>? dto;
        try
        {
            var json = File.ReadAllText(_filePath);
            dto = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, FieldDto>>>(json, ReadOptions);
        }
        catch (JsonException)
        {
            // Malformed file; leave the store empty rather than crash. The
            // user will see "no connections" and can re-import or hand-edit.
            return;
        }
        if (dto is null) return;

        foreach (var (name, fields) in dto)
        {
            if (string.IsNullOrWhiteSpace(name) || fields is null) continue;
            var fieldsDict = new Dictionary<string, ConnectionField>(StringComparer.Ordinal);
            foreach (var (fname, fdto) in fields)
            {
                if (string.IsNullOrWhiteSpace(fname) || fdto is null) continue;

                if (fdto.Value is not null)
                {
                    fieldsDict[fname] = new ConnectionField(fdto.Value, null);
                }
                else if (fdto.FromVault is not null
                         && !string.IsNullOrWhiteSpace(fdto.FromVault.Category)
                         && !string.IsNullOrWhiteSpace(fdto.FromVault.Key))
                {
                    fieldsDict[fname] = new ConnectionField(
                        null,
                        new ConnectionVaultRef(fdto.FromVault.Category!, fdto.FromVault.Key!));
                }
                // else: malformed field, skip silently
            }
            _byName[name] = new Connection(name, fieldsDict);
        }
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var dto = _byName
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Fields.ToDictionary(
                    f => f.Key,
                    f => new FieldDto
                    {
                        Value = f.Value.Value,
                        FromVault = f.Value.FromVault is null
                            ? null
                            : new VaultRefDto
                            {
                                Category = f.Value.FromVault.Category,
                                Key      = f.Value.FromVault.Key,
                            },
                    }),
                StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(dto, WriteOptions);
        File.WriteAllText(_filePath, json);
    }

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class FieldDto
    {
        public string? Value { get; set; }
        public VaultRefDto? FromVault { get; set; }
    }

    private sealed class VaultRefDto
    {
        public string? Category { get; set; }
        public string? Key      { get; set; }
    }
}
