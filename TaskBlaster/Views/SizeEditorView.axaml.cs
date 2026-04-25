using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

public partial class SizeEditorView : UserControl
{
    private readonly TextBox _widthBox;
    private readonly TextBox _heightBox;
    private readonly CheckBox _resizableBox;
    private readonly TextBlock _errorText;

    private IFormDocument? _document;
    private bool _suppress;

    public SizeEditorView()
    {
        InitializeComponent();
        _widthBox     = this.FindControl<TextBox>("WidthBox")!;
        _heightBox    = this.FindControl<TextBox>("HeightBox")!;
        _resizableBox = this.FindControl<CheckBox>("ResizableBox")!;
        _errorText    = this.FindControl<TextBlock>("ErrorText")!;

        _widthBox.TextChanged       += (_, _) => Commit(_widthBox,  isWidth: true);
        _heightBox.TextChanged      += (_, _) => Commit(_heightBox, isWidth: false);
        _resizableBox.IsCheckedChanged += (_, _) => CommitResizable();
    }

    public IFormDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;
            _document = value;
            LoadFromDocument();
        }
    }

    private void LoadFromDocument()
    {
        _suppress = true;
        _widthBox.Text     = _document?.Width  is { } w ? w.ToString(CultureInfo.InvariantCulture) : string.Empty;
        _heightBox.Text    = _document?.Height is { } h ? h.ToString(CultureInfo.InvariantCulture) : string.Empty;
        _resizableBox.IsChecked = _document?.Resizable ?? false;
        ClearError();
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private void CommitResizable()
    {
        if (_suppress || _document is null) return;
        _document.Resizable = _resizableBox.IsChecked == true;
    }

    private void Commit(TextBox box, bool isWidth)
    {
        if (_suppress || _document is null) return;
        var text = box.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (isWidth) _document.Width = null;
            else         _document.Height = null;
            ClearError();
            return;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            ShowError($"{(isWidth ? "Width" : "Height")} must be a positive number.");
            return;
        }

        if (isWidth) _document.Width = parsed;
        else         _document.Height = parsed;
        ClearError();
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        _document.Width = null;
        _document.Height = null;
        // Reset to auto only clears the size; Resizable is a deliberate
        // user choice, leave it alone.
        LoadFromDocument();
    }

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.IsVisible = true;
    }

    private void ClearError()
    {
        _errorText.Text = string.Empty;
        _errorText.IsVisible = false;
    }
}
