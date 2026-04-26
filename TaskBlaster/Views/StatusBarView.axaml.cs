using Avalonia.Controls;
using Avalonia.Media;

namespace TaskBlaster.Views;

public partial class StatusBarView : UserControl
{
    private readonly TextBlock _fileLabel;
    private readonly TextBlock _dirtyLabel;
    private readonly TextBlock _fontSizeLabel;
    private readonly TextBlock _themeLabel;
    private readonly TextBlock _statusLabel;

    public StatusBarView()
    {
        InitializeComponent();
        _fileLabel     = this.FindControl<TextBlock>("FileLabel")!;
        _dirtyLabel    = this.FindControl<TextBlock>("DirtyLabel")!;
        _fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel")!;
        _themeLabel    = this.FindControl<TextBlock>("ThemeLabel")!;
        _statusLabel   = this.FindControl<TextBlock>("StatusLabel")!;
    }

    public string CurrentFile
    {
        get => _fileLabel.Text ?? string.Empty;
        set => _fileLabel.Text = string.IsNullOrEmpty(value) ? "No script selected" : value;
    }

    public string FontSizeText
    {
        get => _fontSizeLabel.Text ?? string.Empty;
        set => _fontSizeLabel.Text = value;
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

    /// <summary>
    /// Show a coloured dirty/saved indicator next to the filename.
    /// Pass null when no file is selected to hide it entirely.
    /// </summary>
    public void SetDirty(bool? isDirty)
    {
        if (isDirty is null)
        {
            _dirtyLabel.IsVisible = false;
            return;
        }
        _dirtyLabel.IsVisible = true;
        _dirtyLabel.Text = "●";
        var key = isDirty == true ? "DangerBrush" : "SuccessBrush";
        ToolTip.SetTip(_dirtyLabel, isDirty == true ? "Unsaved changes" : "Saved");
        if (this.TryFindResource(key, out var brush) && brush is IBrush b)
            _dirtyLabel.Foreground = b;
    }
}
