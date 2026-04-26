using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskBlaster.Connections;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

/// <summary>
/// Two-pane editor for the named-connection config. Connections list on
/// the left, per-connection fields on the right. Persistence is implicit:
/// every committed change writes the whole connection back to the
/// <see cref="IConnectionStore"/>, mirroring the live-edit pattern of
/// the Secrets tab.
/// </summary>
public partial class ConnectionsView : UserControl
{
    private readonly ListBox _list;
    private readonly TextBlock _header;
    private readonly DataGrid _grid;
    private readonly ConnectionsActionsView _toolbarActions;

    private IConnectionStore? _store;
    private IPromptService? _prompts;
    private Action<string>? _log;

    private readonly ObservableCollection<string> _connectionNames = new();
    private readonly ObservableCollection<ConnectionFieldEditor> _fields = new();
    private string? _selectedConnectionName;
    private bool _suppressFieldPersist;

    public ConnectionsView()
    {
        InitializeComponent();
        _list           = this.FindControl<ListBox>("ConnectionsList")!;
        _header         = this.FindControl<TextBlock>("ConnectionHeader")!;
        _grid           = this.FindControl<DataGrid>("FieldsGrid")!;

        _list.ItemsSource = _connectionNames;
        _grid.ItemsSource = _fields;

        _toolbarActions = new ConnectionsActionsView();
        _toolbarActions.AddClicked      += (s, e) => OnAddConnectionClicked(s, new RoutedEventArgs());
        _toolbarActions.DeleteClicked   += (s, e) => OnDeleteConnectionClicked(s, new RoutedEventArgs());
        _toolbarActions.AddFieldClicked += (s, e) => OnAddFieldClicked(s, new RoutedEventArgs());
    }

    /// <summary>The action strip this view contributes to the main toolbar.</summary>
    public Control ToolbarActions => _toolbarActions;

    /// <summary>Wire the view to its dependencies.</summary>
    public void Initialize(IConnectionStore store, IPromptService prompts, Action<string> log)
    {
        _store = store;
        _prompts = prompts;
        _log = log;
        Reload();
    }

    /// <summary>Re-read the store from disk and rebuild the connections list. Preserves the current selection if it still exists.</summary>
    public void Reload()
    {
        if (_store is null) return;
        _store.Reload();
        var keepName = _selectedConnectionName;

        _connectionNames.Clear();
        foreach (var c in _store.List())
            _connectionNames.Add(c.Name);

        if (keepName is not null && _connectionNames.Contains(keepName))
            _list.SelectedItem = keepName;
        else
            _list.SelectedItem = null;
    }

    private void OnConnectionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var name = _list.SelectedItem as string;
        _selectedConnectionName = name;
        _toolbarActions.HasSelection = name is not null;
        LoadFieldsFor(name);
    }

    private void LoadFieldsFor(string? name)
    {
        _suppressFieldPersist = true;
        _fields.Clear();
        if (name is null || _store is null)
        {
            _header.Text = "Pick a connection on the left, or add a new one.";
            _suppressFieldPersist = false;
            return;
        }

        _header.Text = name;
        var conn = _store.Get(name);
        if (conn is not null)
        {
            foreach (var (fname, field) in conn.Fields)
                _fields.Add(ConnectionFieldEditor.FromField(fname, field));
        }
        _suppressFieldPersist = false;
    }

    private async void OnAddConnectionClicked(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _prompts is null) return;

        var raw = await _prompts.InputAsync("New connection", "Connection name:", defaultValue: "");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var name = raw.Trim();

        if (_store.Get(name) is not null)
        {
            await _prompts.MessageAsync("Already exists", $"A connection named '{name}' already exists.");
            return;
        }

        _store.Save(new Connection(name, new Dictionary<string, ConnectionField>()));
        _log?.Invoke($"Connection '{name}' created.");
        Reload();
        _list.SelectedItem = name;
    }

    private async void OnDeleteConnectionClicked(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _prompts is null) return;
        var name = _selectedConnectionName;
        if (name is null) return;

        var ok = await _prompts.ConfirmAsync(
            "Delete connection",
            $"Delete connection '{name}'? Vault entries it pointed at are NOT removed.");
        if (!ok) return;

        _store.Remove(name);
        _log?.Invoke($"Connection '{name}' deleted.");
        _selectedConnectionName = null;
        Reload();
    }

    private void OnAddFieldClicked(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _selectedConnectionName is null) return;
        // Choose a default name that doesn't collide; user typically renames immediately.
        var n = 1;
        string newName;
        do { newName = n == 1 ? "field" : $"field{n}"; n++; }
        while (_fields.Any(f => string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)));

        _fields.Add(new ConnectionFieldEditor { Name = newName, Mode = ConnectionFieldMode.Plaintext });
        PersistCurrentConnection();
    }

    private void OnRemoveFieldClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not ConnectionFieldEditor field) return;
        _fields.Remove(field);
        PersistCurrentConnection();
    }

    private void OnFieldNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFieldPersist) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ConnectionFieldEditor field) return;
        var newText = tb.Text ?? "";
        if (string.Equals(field.Name, newText, StringComparison.Ordinal)) return;
        field.Name = newText;
        PersistCurrentConnection();
    }

    private void OnPlaintextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFieldPersist) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ConnectionFieldEditor field) return;
        var newText = tb.Text ?? "";
        if (string.Equals(field.PlaintextValue, newText, StringComparison.Ordinal)) return;
        field.PlaintextValue = newText;
        PersistCurrentConnection();
    }

    private void OnVaultCategoryChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFieldPersist) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ConnectionFieldEditor field) return;
        var newText = tb.Text ?? "";
        if (string.Equals(field.VaultCategory, newText, StringComparison.Ordinal)) return;
        field.VaultCategory = newText;
        PersistCurrentConnection();
    }

    private void OnVaultKeyChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFieldPersist) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ConnectionFieldEditor field) return;
        var newText = tb.Text ?? "";
        if (string.Equals(field.VaultKey, newText, StringComparison.Ordinal)) return;
        field.VaultKey = newText;
        PersistCurrentConnection();
    }

    private void OnModeAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not ConnectionFieldEditor field) return;
        cb.SelectedIndex = field.Mode == ConnectionFieldMode.FromVault ? 1 : 0;
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressFieldPersist) return;
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not ConnectionFieldEditor field) return;

        var wantFromVault = cb.SelectedIndex == 1;
        var newMode = wantFromVault ? ConnectionFieldMode.FromVault : ConnectionFieldMode.Plaintext;
        if (field.Mode == newMode) return;
        field.Mode = newMode;
        PersistCurrentConnection();
    }

    private void PersistCurrentConnection()
    {
        if (_suppressFieldPersist) return;
        if (_store is null || _selectedConnectionName is null) return;

        var dict = new Dictionary<string, ConnectionField>(StringComparer.Ordinal);
        foreach (var f in _fields)
        {
            if (string.IsNullOrWhiteSpace(f.Name)) continue;
            // Last-write-wins on duplicate names — the user will see them
            // in the grid and can rename.
            dict[f.Name] = f.ToField();
        }
        _store.Save(new Connection(_selectedConnectionName, dict));
    }
}
