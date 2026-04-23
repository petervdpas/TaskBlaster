using Avalonia.Controls;

namespace TaskBlaster.Views;

public partial class StatusBarView : UserControl
{
    private readonly TextBlock _fileLabel;
    private readonly TextBlock _themeLabel;
    private readonly TextBlock _statusLabel;

    public StatusBarView()
    {
        InitializeComponent();
        _fileLabel = this.FindControl<TextBlock>("FileLabel")!;
        _themeLabel = this.FindControl<TextBlock>("ThemeLabel")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;
    }

    public string CurrentFile
    {
        get => _fileLabel.Text ?? string.Empty;
        set => _fileLabel.Text = string.IsNullOrEmpty(value) ? "No script selected" : value;
    }

    public string ThemeName
    {
        get => _themeLabel.Text ?? string.Empty;
        set => _themeLabel.Text = value;
    }

    public string Status
    {
        get => _statusLabel.Text ?? string.Empty;
        set => _statusLabel.Text = value;
    }
}
