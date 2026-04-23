using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;

namespace TaskBlaster.Views;

public partial class SidebarView : UserControl
{
    private readonly ListBox _list;
    private string? _folder;

    public event EventHandler<string>? ScriptSelected;

    public SidebarView()
    {
        InitializeComponent();
        _list = this.FindControl<ListBox>("ScriptList")!;
    }

    public string? Folder
    {
        get => _folder;
        set { _folder = value; Refresh(); }
    }

    public void Refresh()
    {
        if (_folder is null || !Directory.Exists(_folder))
        {
            _list.ItemsSource = Array.Empty<string>();
            return;
        }

        _list.ItemsSource = Directory
            .EnumerateFiles(_folder, "*.csx", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Select(string fileName)
    {
        _list.SelectedItem = fileName;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_folder is null) return;
        if (_list.SelectedItem is not string name) return;
        var path = Path.Combine(_folder, name);
        if (File.Exists(path)) ScriptSelected?.Invoke(this, path);
    }
}
