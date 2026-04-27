using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;

namespace TaskBlaster.Views;

public partial class SidebarView : UserControl
{
    private readonly ListBox _list;
    private readonly TextBlock _header;
    private readonly FilterBoxView _filter;
    private string? _folder;
    private string _pattern = "*.csx";
    private List<string> _allFiles = new();

    public event EventHandler<string>? ScriptSelected;

    public SidebarView()
    {
        InitializeComponent();
        _list   = this.FindControl<ListBox>("ScriptList")!;
        _header = this.FindControl<TextBlock>("HeaderLabel")!;
        _filter = this.FindControl<FilterBoxView>("Filter")!;
        _filter.FilterChanged += (_, _) => ApplyFilter();
    }

    public string Header
    {
        get => _header.Text ?? string.Empty;
        set => _header.Text = value;
    }

    public string? Folder
    {
        get => _folder;
        set { _folder = value; Refresh(); }
    }

    /// <summary>File-name glob used to filter the folder (e.g. "*.csx", "*.json").</summary>
    public string Pattern
    {
        get => _pattern;
        set { _pattern = value; Refresh(); }
    }

    public void Refresh()
    {
        if (_folder is null || !Directory.Exists(_folder))
        {
            _allFiles = new();
            _list.ItemsSource = Array.Empty<string>();
            return;
        }

        _allFiles = Directory
            .EnumerateFiles(_folder, _pattern, SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ApplyFilter();
    }

    public void Select(string fileName)
    {
        _list.SelectedItem = fileName;
    }

    private void ApplyFilter()
    {
        _list.ItemsSource = _allFiles.Where(f => _filter.Matches(f)).ToList();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_folder is null) return;
        if (_list.SelectedItem is not string name) return;
        var path = Path.Combine(_folder, name);
        if (File.Exists(path)) ScriptSelected?.Invoke(this, path);
    }
}
