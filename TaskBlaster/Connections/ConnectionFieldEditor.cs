using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskBlaster.Connections;

/// <summary>
/// In-memory editor model for one field of a <see cref="Connection"/>.
/// Implements INPC so the value-column DataTemplate can flip its
/// visible editor when the user switches between Plaintext and FromVault.
/// Round-trips to <see cref="ConnectionField"/> at save-time via
/// <see cref="ToField"/> / <see cref="FromField"/>.
/// </summary>
public sealed class ConnectionFieldEditor : INotifyPropertyChanged
{
    private string _name = "";
    private ConnectionFieldMode _mode = ConnectionFieldMode.Plaintext;
    private string _plaintextValue = "";
    private string _vaultCategory = "";
    private string _vaultKey = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public ConnectionFieldMode Mode
    {
        get => _mode;
        set
        {
            if (SetField(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsPlaintext));
                OnPropertyChanged(nameof(IsFromVault));
            }
        }
    }

    public string PlaintextValue
    {
        get => _plaintextValue;
        set => SetField(ref _plaintextValue, value);
    }

    public string VaultCategory
    {
        get => _vaultCategory;
        set => SetField(ref _vaultCategory, value);
    }

    public string VaultKey
    {
        get => _vaultKey;
        set => SetField(ref _vaultKey, value);
    }

    /// <summary>True when the value editor should show the plaintext TextBox.</summary>
    public bool IsPlaintext => _mode == ConnectionFieldMode.Plaintext;

    /// <summary>True when the value editor should show the (category, key) pair.</summary>
    public bool IsFromVault => _mode == ConnectionFieldMode.FromVault;

    /// <summary>Build an editor row from a persisted <see cref="ConnectionField"/>.</summary>
    public static ConnectionFieldEditor FromField(string name, ConnectionField field)
    {
        var editor = new ConnectionFieldEditor { _name = name };
        if (field.FromVault is not null)
        {
            editor._mode = ConnectionFieldMode.FromVault;
            editor._vaultCategory = field.FromVault.Category;
            editor._vaultKey      = field.FromVault.Key;
        }
        else
        {
            editor._mode = ConnectionFieldMode.Plaintext;
            editor._plaintextValue = field.Value ?? "";
        }
        return editor;
    }

    /// <summary>Build a persistable <see cref="ConnectionField"/> from the current editor state.</summary>
    public ConnectionField ToField() => _mode == ConnectionFieldMode.FromVault
        ? ConnectionField.OfVault(_vaultCategory, _vaultKey)
        : ConnectionField.Plaintext(_plaintextValue);

    private bool SetField<T>(ref T storage, T value, [CallerMemberName] string? property = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(property);
        return true;
    }

    private void OnPropertyChanged(string? property)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public enum ConnectionFieldMode
{
    Plaintext,
    FromVault,
}
