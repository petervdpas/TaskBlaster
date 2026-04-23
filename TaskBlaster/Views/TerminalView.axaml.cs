using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

public partial class TerminalView : UserControl
{
    private readonly TextBox _output;

    public TerminalView()
    {
        InitializeComponent();
        _output = this.FindControl<TextBox>("Output")!;
    }

    public void Log(string line)
    {
        _output.Text = string.IsNullOrEmpty(_output.Text)
            ? line
            : _output.Text + Environment.NewLine + line;
        _output.CaretIndex = _output.Text.Length;
    }

    public void Clear() => _output.Text = string.Empty;

    private void OnClearClicked(object? sender, RoutedEventArgs e) => Clear();
}
