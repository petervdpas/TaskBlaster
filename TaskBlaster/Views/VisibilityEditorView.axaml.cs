using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

public partial class VisibilityEditorView : UserControl
{
    private readonly ItemsControl _list;
    private IFormDocument? _document;

    public VisibilityEditorView()
    {
        InitializeComponent();
        _list = this.FindControl<ItemsControl>("RulesList")!;
    }

    public IFormDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;
            if (_document is not null) _document.VisibilityChanged -= OnChanged;
            _document = value;
            if (_document is not null) _document.VisibilityChanged += OnChanged;
            Rebuild();
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var items = new List<Control>();
        if (_document is not null)
            foreach (var rule in _document.Visibility)
                items.Add(BuildRow(rule));
        _list.ItemsSource = items;
    }

    /// <summary>
    /// Row shape: [field] [op ▼] [value]  →  [action ▼] [targets csv]  [×]
    /// </summary>
    private Control BuildRow(VisibilityRuleEditor rule)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,80,120,Auto,90,*,Auto")
        };

        // Controller field
        var fieldBox = new TextBox { Text = rule.Field, Margin = new Thickness(0, 0, 6, 0) };
        ToolTip.SetTip(fieldBox, "Controller field key");
        fieldBox.TextChanged += (_, _) =>
        {
            rule.Field = fieldBox.Text ?? "";
            _document?.MarkVisibilityChanged();
        };
        Grid.SetColumn(fieldBox, 0);
        grid.Children.Add(fieldBox);

        // Operator: eq / neq
        var opBox = new ComboBox { Margin = new Thickness(0, 0, 6, 0) };
        opBox.Items.Add(new ComboBoxItem { Content = "equals" });
        opBox.Items.Add(new ComboBoxItem { Content = "not equal" });
        opBox.SelectedIndex = rule.Neq is not null ? 1 : 0; // default: eq unless neq is set
        ToolTip.SetTip(opBox, "Trigger operator");
        opBox.SelectionChanged += (_, _) =>
        {
            if (opBox.SelectedIndex == 0)
            {
                rule.Eq = rule.Eq ?? rule.Neq; rule.Neq = null;
            }
            else
            {
                rule.Neq = rule.Neq ?? rule.Eq; rule.Eq = null;
            }
            _document?.MarkVisibilityChanged();
        };
        Grid.SetColumn(opBox, 1);
        grid.Children.Add(opBox);

        // Value
        var valueBox = new TextBox
        {
            Text = rule.Eq ?? rule.Neq ?? "",
            Margin = new Thickness(0, 0, 8, 0),
        };
        ToolTip.SetTip(valueBox, "Trigger value");
        valueBox.TextChanged += (_, _) =>
        {
            if (opBox.SelectedIndex == 0) rule.Eq  = valueBox.Text;
            else                          rule.Neq = valueBox.Text;
            _document?.MarkVisibilityChanged();
        };
        Grid.SetColumn(valueBox, 2);
        grid.Children.Add(valueBox);

        // Arrow
        var arrow = new TextBlock { Text = "→", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(arrow, 3);
        grid.Children.Add(arrow);

        // Action: show / hide
        var actionBox = new ComboBox { Margin = new Thickness(0, 0, 6, 0) };
        actionBox.Items.Add(new ComboBoxItem { Content = "show" });
        actionBox.Items.Add(new ComboBoxItem { Content = "hide" });
        actionBox.SelectedIndex = rule.Hide.Count > 0 ? 1 : 0;
        ToolTip.SetTip(actionBox, "Show or hide the target fields when triggered");
        actionBox.SelectionChanged += (_, _) =>
        {
            SwapShowHide(rule, showing: actionBox.SelectedIndex == 0);
            _document?.MarkVisibilityChanged();
        };
        Grid.SetColumn(actionBox, 4);
        grid.Children.Add(actionBox);

        // Targets (comma-separated)
        var targetsBox = new TextBox
        {
            Text = string.Join(", ", actionBox.SelectedIndex == 0 ? rule.Show : rule.Hide),
            Margin = new Thickness(0, 0, 6, 0),
        };
        ToolTip.SetTip(targetsBox, "Comma-separated field keys to show/hide");
        targetsBox.TextChanged += (_, _) =>
        {
            var targets = (targetsBox.Text ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            var list = actionBox.SelectedIndex == 0 ? rule.Show : rule.Hide;
            list.Clear();
            foreach (var t in targets) list.Add(t);
            _document?.MarkVisibilityChanged();
        };
        Grid.SetColumn(targetsBox, 5);
        grid.Children.Add(targetsBox);

        // Remove
        var remove = new Button { Content = "×", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(remove, "Remove rule");
        remove.Click += (_, _) => _document?.RemoveVisibilityRule(rule);
        Grid.SetColumn(remove, 6);
        grid.Children.Add(remove);

        return grid;
    }

    private static void SwapShowHide(VisibilityRuleEditor rule, bool showing)
    {
        if (showing)
        {
            // Move Hide → Show (if user flipped action)
            foreach (var t in rule.Hide.ToList())
            {
                if (!rule.Show.Contains(t)) rule.Show.Add(t);
            }
            rule.Hide.Clear();
        }
        else
        {
            foreach (var t in rule.Show.ToList())
            {
                if (!rule.Hide.Contains(t)) rule.Hide.Add(t);
            }
            rule.Show.Clear();
        }
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _document?.AddVisibilityRule();
}
