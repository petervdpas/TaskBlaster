using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views.FieldEditors;

public partial class OptionsPropertyEditor : UserControl, IFieldPropertyEditor
{
    private readonly RadioButton _staticRadio;
    private readonly RadioButton _vaultRadio;
    private readonly StackPanel _categoryPanel;

    private readonly ListBox _optionList;
    private readonly TextBox _valueBox;
    private readonly ComboBox _valueCombo;
    private readonly TextBox _labelBox;

    private readonly ComboBox _categoryCombo;
    private readonly TextBlock _vaultHint;

    private readonly IVaultService? _vault;
    private readonly Func<CancellationToken, Task>? _ensureUnlocked;

    private FieldEditor? _field;
    private OptionEditor? _selected;
    private bool _suppress;

    private readonly ObservableCollection<string> _displayItems = new();

    public event EventHandler? Changed;

    public OptionsPropertyEditor() : this(null, null) { }

    /// <summary>
    /// Creates the editor bound to an <see cref="IVaultService"/> and an
    /// optional unlock callback. When the user picks "From vault" the
    /// Value box turns into a ComboBox of keys from the chosen category,
    /// and Label stays a free-text field. On a locked vault
    /// <paramref name="ensureUnlocked"/> is awaited to pop the password
    /// dialog — same flow scripts use.
    ///
    /// The parameterless overload exists only for XAML design-time
    /// previews; the real designer path goes through
    /// <see cref="FieldPropertyEditorFactory.Create"/>.
    /// </summary>
    public OptionsPropertyEditor(IVaultService? vault, Func<CancellationToken, Task>? ensureUnlocked)
    {
        _vault = vault;
        _ensureUnlocked = ensureUnlocked;
        InitializeComponent();

        _staticRadio   = this.FindControl<RadioButton>("StaticRadio")!;
        _vaultRadio    = this.FindControl<RadioButton>("VaultRadio")!;
        _categoryPanel = this.FindControl<StackPanel>("CategoryPanel")!;

        _optionList    = this.FindControl<ListBox>("OptionList")!;
        _valueBox      = this.FindControl<TextBox>("OptionValueBox")!;
        _valueCombo    = this.FindControl<ComboBox>("OptionValueCombo")!;
        _labelBox      = this.FindControl<TextBox>("OptionLabelBox")!;

        _categoryCombo = this.FindControl<ComboBox>("CategoryCombo")!;
        _vaultHint     = this.FindControl<TextBlock>("VaultHint")!;

        _valueBox.TextChanged += (_, _) => CommitValueFromTextBox();
        _labelBox.TextChanged += (_, _) => CommitLabel();

        _optionList.ItemsSource = _displayItems;
    }

    public void Bind(FieldEditor field)
    {
        _field = field;
        _selected = null;
        _suppress = true;

        _valueBox.Text = "";
        _labelBox.Text = "";
        _valueCombo.SelectedItem = null;
        RefreshOptionList();

        var fromVault = field.OptionsSource is { Source: "vault" };
        _staticRadio.IsChecked = !fromVault;
        _vaultRadio.IsChecked  =  fromVault;
        _categoryPanel.IsVisible = fromVault;
        SetValueEditor(fromVault);

        if (fromVault) _ = LoadCategoriesAsync(preselect: field.OptionsSource?.Category);

        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private void SetValueEditor(bool vaultMode)
    {
        _valueBox.IsVisible   = !vaultMode;
        _valueCombo.IsVisible =  vaultMode;
    }

    // =========================================================
    // Source toggle
    // =========================================================

    private void OnStaticSelected(object? sender, RoutedEventArgs e)
    {
        if (_suppress || _field is null) return;
        if (_staticRadio.IsChecked != true) return;

        _field.OptionsSource = null;
        _categoryPanel.IsVisible = false;
        SetValueEditor(vaultMode: false);

        // Reflect the currently-selected option's value into the TextBox.
        _suppress = true;
        _valueBox.Text = _selected?.Value ?? "";
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnVaultSelected(object? sender, RoutedEventArgs e)
    {
        if (_suppress || _field is null) return;
        if (_vaultRadio.IsChecked != true) return;

        _field.OptionsSource ??= new OptionsSourceEditor { Source = "vault", Category = "" };
        _field.OptionsSource.Source = "vault";

        _categoryPanel.IsVisible = true;
        SetValueEditor(vaultMode: true);

        _ = LoadCategoriesAsync(preselect: _field.OptionsSource.Category);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadCategoriesAsync(string? preselect)
    {
        _suppress = true;
        try
        {
            if (_vault is null)
            {
                _categoryCombo.ItemsSource = Array.Empty<string>();
                _vaultHint.Text = "Vault not available.";
                return;
            }

            // Pop the password prompt via the owner callback. No-op if already unlocked.
            if (!_vault.IsUnlocked && _ensureUnlocked is not null)
                await _ensureUnlocked(default);

            if (!_vault.IsUnlocked)
            {
                _categoryCombo.ItemsSource = Array.Empty<string>();
                _vaultHint.Text = "Vault is locked. Toggle source again to retry unlock.";
                return;
            }

            var cats = await _vault.GetCategoriesAsync();
            _categoryCombo.ItemsSource = cats;
            _vaultHint.Text = cats.Count == 0
                ? "No categories yet. Add a secret in the Secrets tab first."
                : "Each option's Value is a key from this category.";

            if (!string.IsNullOrEmpty(preselect))
                _categoryCombo.SelectedItem = cats.FirstOrDefault(c =>
                    string.Equals(c, preselect, StringComparison.OrdinalIgnoreCase));

            // Always refresh the Value ComboBox against the (possibly just-set) category.
            await RefreshValueComboAsync();
        }
        catch (Exception ex)
        {
            _vaultHint.Text = $"Could not load categories: {ex.Message}";
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
        }
    }

    private async void OnCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _field is null) return;
        var cat = _categoryCombo.SelectedItem as string ?? "";
        _field.OptionsSource ??= new OptionsSourceEditor { Source = "vault" };
        _field.OptionsSource.Category = cat;
        await RefreshValueComboAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshValueComboAsync()
    {
        var cat = _categoryCombo.SelectedItem as string ?? "";
        if (_vault is null || !_vault.IsUnlocked || string.IsNullOrWhiteSpace(cat))
        {
            _valueCombo.ItemsSource = Array.Empty<string>();
            return;
        }

        var all = await _vault.ListAsync();
        var keys = all
            .Where(e => string.Equals(e.Category, cat, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Key)
            .ToList();

        _suppress = true;
        _valueCombo.ItemsSource = keys;
        _valueCombo.SelectedItem = _selected is not null
            ? keys.FirstOrDefault(k => string.Equals(k, _selected.Value, StringComparison.Ordinal))
            : null;
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private void OnValueComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _selected is null) return;
        var picked = _valueCombo.SelectedItem as string ?? "";
        _selected.Value = picked;
        // Label defaults to the key name the first time the user picks from
        // the vault — saves a click. They can still overwrite it.
        if (string.IsNullOrWhiteSpace(_selected.Label))
        {
            _selected.Label = picked;
            _suppress = true;
            _labelBox.Text = picked;
            Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
        }
        UpdateDisplayForSelected();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // =========================================================
    // Options list
    // =========================================================

    private void RefreshOptionList()
    {
        _displayItems.Clear();
        if (_field is null) return;
        foreach (var o in _field.Options)
            _displayItems.Add(FormatOption(o));
    }

    private void UpdateDisplayForSelected()
    {
        if (_field is null || _selected is null) return;
        var idx = _field.Options.IndexOf(_selected);
        if (idx < 0 || idx >= _displayItems.Count) return;

        // Replacing the string at idx makes ListBox's SelectedItem reference
        // stale; selection drops and a phantom SelectionChanged(null) fires.
        // Suppress the spurious event and re-pin the selection by index.
        var wasSuppressed = _suppress;
        _suppress = true;
        _displayItems[idx] = FormatOption(_selected);
        _optionList.SelectedIndex = idx;
        if (!wasSuppressed)
            Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private static string FormatOption(OptionEditor o) =>
        string.IsNullOrEmpty(o.Label) ? o.Value : $"{o.Label} ({o.Value})";

    private void OnOptionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _field is null) return;
        var idx = _optionList.SelectedIndex;
        var newSelected = idx >= 0 && idx < _field.Options.Count ? _field.Options[idx] : null;
        if (ReferenceEquals(newSelected, _selected)) return;

        _selected = newSelected;
        _suppress = true;
        _labelBox.Text = _selected?.Label ?? "";
        if (_valueCombo.IsVisible)
        {
            _valueCombo.SelectedItem = _selected is null
                ? null
                : (_valueCombo.ItemsSource as IEnumerable<string>)?
                    .FirstOrDefault(k => string.Equals(k, _selected.Value, StringComparison.Ordinal));
        }
        else
        {
            _valueBox.Text = _selected?.Value ?? "";
        }
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private void CommitValueFromTextBox()
    {
        if (_suppress || _selected is null) return;
        _selected.Value = _valueBox.Text ?? "";
        UpdateDisplayForSelected();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CommitLabel()
    {
        if (_suppress || _selected is null) return;
        _selected.Label = _labelBox.Text;
        UpdateDisplayForSelected();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAddOption(object? sender, RoutedEventArgs e)
    {
        if (_field is null) return;
        var o = new OptionEditor { Value = "value", Label = "Label" };
        _field.Options.Add(o);
        RefreshOptionList();
        _optionList.SelectedIndex = _field.Options.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemoveOption(object? sender, RoutedEventArgs e)
    {
        if (_field is null || _selected is null) return;
        _field.Options.Remove(_selected);
        _selected = null;
        RefreshOptionList();

        _suppress = true;
        _valueBox.Text = "";
        _labelBox.Text = "";
        _valueCombo.SelectedItem = null;
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
