using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TaskBlaster.Views.Terminal;

namespace TaskBlaster.Views;

public partial class TerminalView : UserControl
{
    private readonly ObservableCollection<TerminalItem> _items = new();
    private readonly ScrollViewer _scroll;
    private readonly ItemsControl _list;

    public TerminalView()
    {
        InitializeComponent();
        _list = this.FindControl<ItemsControl>("OutputItems")!;
        _scroll = this.FindControl<ScrollViewer>("OutputScroll")!;

        _list.ItemsSource = _items;
        _list.ItemTemplate = new FuncDataTemplate<TerminalItem>(
            match: _ => true,
            build: (item, _) => Render(item));
    }

    /// <summary>
    /// Append a single line to the terminal. If the line is a recognised
    /// <c>$blast</c>-discriminated JSON message it renders as a widget;
    /// otherwise it renders as a selectable monospace text line.
    /// </summary>
    public void Log(string line)
    {
        var item = BlastParser.ParseOrText(line);
        _items.Add(item);
        ScrollToEnd();
    }

    public void Clear() => _items.Clear();

    private void OnClearClicked(object? sender, RoutedEventArgs e) => Clear();

    private void ScrollToEnd()
        => Dispatcher.UIThread.Post(() => _scroll.ScrollToEnd(), DispatcherPriority.Background);

    // ---------- per-item widget builders ----------

    private static Control Render(TerminalItem? item) => item switch
    {
        TextItem t      => BuildText(t),
        HeadingItem h   => BuildHeading(h),
        StatusItem s    => BuildStatus(s),
        TableItem tab   => BuildTable(tab),
        KvItem kv       => BuildKv(kv),
        _               => new TextBlock { Text = string.Empty },
    };

    private static Control BuildText(TextItem t) =>
        new SelectableTextBlock
        {
            Text = t.Line,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,Monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
        };

    private static Control BuildHeading(HeadingItem h)
    {
        var size = h.Level switch
        {
            1 => 18.0, 2 => 16.0, 3 => 14.5, 4 => 13.0, 5 => 12.5, _ => 12.0,
        };
        return new TextBlock
        {
            Text = h.Text,
            FontSize = size,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 4),
        };
    }

    private static Control BuildStatus(StatusItem s)
    {
        var (glyph, brush) = s.Level switch
        {
            TerminalStatusLevel.Ok    => ("✓", Brushes.SeaGreen),
            TerminalStatusLevel.Warn  => ("⚠", Brushes.DarkOrange),
            TerminalStatusLevel.Error => ("✗", Brushes.IndianRed),
            _                         => ("•", Brushes.SteelBlue),
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };
        panel.Children.Add(new TextBlock { Text = glyph, Foreground = brush, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 6, 0) });
        panel.Children.Add(new TextBlock { Text = s.Text });
        return panel;
    }

    private static Control BuildTable(TableItem t)
    {
        var outer = new StackPanel { Margin = new Thickness(0, 6) };

        if (!string.IsNullOrEmpty(t.Title))
        {
            outer.Children.Add(new TextBlock
            {
                Text = t.Title,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        // Project rows into a ViewModel exposing an indexed Cells property; DataGrid
        // bindings of the form "Cells[c]" hit the indexer at render-time.
        var rows = t.Rows.Select(r => new TableRowViewModel(r)).ToList();

        var dataGrid = new DataGrid
        {
            ItemsSource = rows,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserResizeColumns = true,
            CanUserReorderColumns = false,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            // Hug the natural content width — don't stretch into the panel.
            HorizontalAlignment = HorizontalAlignment.Left,
            // Cap the inline render height so very large datasets get an internal
            // scrollbar; the outer terminal scrolls everything else as a single column.
            MaxHeight = 400,
        };

        for (var c = 0; c < t.Headers.Count; c++)
        {
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = t.Headers[c],
                Binding = new Binding($"Cells[{c}]"),
                Width = DataGridLength.Auto,
            });
        }

        ApplyAlternatingRows(dataGrid);
        outer.Children.Add(WrapInBorder(dataGrid));
        return outer;
    }

    /// <summary>Row VM exposing an indexed cell list so DataGrid columns can bind to <c>Cells[i]</c>.</summary>
    private sealed class TableRowViewModel
    {
        public IReadOnlyList<string?> Cells { get; }
        public TableRowViewModel(IReadOnlyList<string?> cells) => Cells = cells;
    }

    private static Control BuildKv(KvItem kv)
    {
        var outer = new StackPanel { Margin = new Thickness(0, 6) };

        if (!string.IsNullOrEmpty(kv.Title))
        {
            outer.Children.Add(new TextBlock
            {
                Text = kv.Title,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        var rows = kv.Pairs
            .Select(p => new KvRowViewModel(p.Key, p.Value ?? "(null)"))
            .ToList();

        var dataGrid = new DataGrid
        {
            ItemsSource = rows,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserResizeColumns = true,
            CanUserReorderColumns = false,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxHeight = 400,
        };

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Key",
            Binding = new Binding(nameof(KvRowViewModel.Key)),
            Width = DataGridLength.Auto,
        });
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(KvRowViewModel.Value)),
            Width = DataGridLength.Auto,
        });

        ApplyAlternatingRows(dataGrid);
        outer.Children.Add(WrapInBorder(dataGrid));
        return outer;
    }

    /// <summary>Per-row striping via the <c>LoadingRow</c> event (Avalonia DataGrid has no built-in AlternatingRowBackground).</summary>
    private static void ApplyAlternatingRows(DataGrid dataGrid)
    {
        dataGrid.LoadingRow += (_, e) =>
        {
            e.Row.Background = e.Row.Index % 2 == 1
                ? AlternatingRowBrush
                : Brushes.Transparent;
        };
    }

    private static readonly IBrush GridFrameBrush =
        new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80));

    private static readonly IBrush AlternatingRowBrush =
        new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80));

    /// <summary>Wraps a DataGrid in a Border so the outer frame is consistent regardless of the DataGrid's own template.</summary>
    private static Border WrapInBorder(Control inner) => new()
    {
        BorderBrush = GridFrameBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(2),
        ClipToBounds = true,
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = inner,
    };

    /// <summary>Row VM for the kv panel's two-column DataGrid.</summary>
    private sealed class KvRowViewModel
    {
        public string Key { get; }
        public string? Value { get; }
        public KvRowViewModel(string key, string? value) { Key = key; Value = value; }
    }
}
