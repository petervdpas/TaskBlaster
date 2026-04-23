using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TaskBlaster.Dialogs;

public partial class ConfigDialog : Window
{
    private readonly TextBox _folderBox;

    public ConfigDialog() : this(string.Empty) { }

    public ConfigDialog(string currentFolder)
    {
        InitializeComponent();
        _folderBox = this.FindControl<TextBox>("FolderBox")!;
        _folderBox.Text = currentFolder;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close((string?)null);
        };
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var sp = StorageProvider;
        if (sp is null) return;

        var startFolder = !string.IsNullOrWhiteSpace(_folderBox.Text)
            ? await sp.TryGetFolderFromPathAsync(_folderBox.Text)
            : null;

        var result = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select scripts folder",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder
        });

        if (result.Count == 0) return;
        _folderBox.Text = result[0].Path.LocalPath;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var chosen = (_folderBox.Text ?? string.Empty).Trim();
        Close(string.IsNullOrEmpty(chosen) ? null : chosen);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((string?)null);
}
