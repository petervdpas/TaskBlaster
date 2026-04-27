using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskBlaster.Externals;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Outcome of <see cref="ExternalValidationDialog"/>. The caller decides
/// what to do based on which button the user clicked.
/// </summary>
public enum ExternalValidationChoice
{
    Cancel,
    Add,
    AddAnyway,
}

/// <summary>One row in the per-DLL section of the validation dialog.</summary>
public sealed class ValidationReportRow
{
    public string AssemblyName    { get; set; } = "";
    public string AssemblyVersion { get; set; } = "";
    public bool   HasNoIssues     { get; set; }
    public List<ValidationIssueRow> Issues { get; set; } = new();
}

/// <summary>One issue line under a <see cref="ValidationReportRow"/>.</summary>
public sealed class ValidationIssueRow
{
    public string Symbol   { get; set; } = "";
    public string BrushKey { get; set; } = "";
    public string Message  { get; set; } = "";
}

/// <summary>
/// Modal that renders a list of <see cref="AssemblyValidationReport"/>s
/// — one per DLL in the candidate package — colour-coded by severity, with
/// three exit buttons: <c>Add</c> (only enabled when no errors),
/// <c>Add anyway</c> (only visible when there are errors), and
/// <c>Cancel</c>.
/// </summary>
public partial class ExternalValidationDialog : Window
{
    private readonly Button _addButton;
    private readonly Button _addAnywayButton;
    private readonly TextBlock _headerLabel;
    private readonly ItemsControl _reportsList;

    public ExternalValidationDialog()
    {
        InitializeComponent();
        _addButton       = this.FindControl<Button>("AddButton")!;
        _addAnywayButton = this.FindControl<Button>("AddAnywayButton")!;
        _headerLabel     = this.FindControl<TextBlock>("HeaderLabel")!;
        _reportsList     = this.FindControl<ItemsControl>("ReportsList")!;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(ExternalValidationChoice.Cancel); };
    }

    /// <summary>
    /// Set the dialog's contents. Call once after construction; the dialog
    /// renders the reports and toggles button visibility based on whether
    /// any errors were found.
    /// </summary>
    public void SetContent(string header, IReadOnlyList<AssemblyValidationReport> reports)
    {
        _headerLabel.Text = header;
        _reportsList.ItemsSource = reports.Select(ToRow).ToList();

        var hasErrors = reports.Any(r => r.HasErrors);
        _addButton.IsVisible       = !hasErrors;
        _addAnywayButton.IsVisible = hasErrors;
    }

    private static ValidationReportRow ToRow(AssemblyValidationReport r) => new()
    {
        AssemblyName    = r.AssemblyName,
        AssemblyVersion = r.AssemblyVersion,
        HasNoIssues     = r.Issues.Count == 0,
        Issues = r.Issues.Select(i => new ValidationIssueRow
        {
            Symbol    = i.Level == IssueLevel.Error ? "✗" : "⚠",
            BrushKey  = i.Level == IssueLevel.Error ? "DangerBrush" : "WarningBrush",
            Message   = i.Message,
        }).ToList(),
    };

    private void OnAdd(object? sender, RoutedEventArgs e)        => Close(ExternalValidationChoice.Add);
    private void OnAddAnyway(object? sender, RoutedEventArgs e)  => Close(ExternalValidationChoice.AddAnyway);
    private void OnCancel(object? sender, RoutedEventArgs e)     => Close(ExternalValidationChoice.Cancel);
}

/// <summary>Looks up a brush by resource key at bind time so the IssueRow can name colours by string.</summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key) return AvaloniaProperty.UnsetValue;
        if (Application.Current is null) return AvaloniaProperty.UnsetValue;
        return Application.Current.TryFindResource(key, out var brush) ? brush : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}
