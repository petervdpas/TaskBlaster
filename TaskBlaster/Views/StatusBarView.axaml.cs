using Avalonia.Controls;
using Avalonia.Media;

namespace TaskBlaster.Views;

public enum StatusLevel { Normal, Error }

public partial class StatusBarView : UserControl
{
    private readonly TextBlock _fileLabel;
    private readonly TextBlock _dirtyLabel;
    private readonly Avalonia.Controls.Shapes.Rectangle _dirtyDivider;
    private readonly TextBlock _fontSizeLabel;
    private readonly TextBlock _themeLabel;
    private readonly TextBlock _statusLabel;

    public StatusBarView()
    {
        InitializeComponent();
        _fileLabel     = this.FindControl<TextBlock>("FileLabel")!;
        _dirtyLabel    = this.FindControl<TextBlock>("DirtyLabel")!;
        _dirtyDivider  = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("DirtyDivider")!;
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
        set => SetStatus(value, StatusLevel.Normal);
    }

    /// <summary>
    /// Set the status text along with a severity level so the label can be
    /// coloured (red for errors, amber for warnings, default otherwise).
    /// </summary>
    public void SetStatus(string text, StatusLevel level)
    {
        _statusLabel.Text = text;

        var brushKey = level == StatusLevel.Error
            ? "DangerBrush"
            : "SystemControlForegroundBaseMediumBrush";

        if (this.TryFindResource(brushKey, out var brush) && brush is IBrush b)
            _statusLabel.Foreground = b;
    }

    /// <summary>
    /// Show a coloured dirty/saved indicator next to the filename. Pass null
    /// when no file is selected to render the dot in the muted default colour.
    /// </summary>
    public void SetDirty(bool? isDirty)
    {
        _dirtyLabel.IsVisible = true;
        _dirtyDivider.IsVisible = true;
        _dirtyLabel.Text = "●";

        string brushKey;
        string? tip;
        if (isDirty is null)        { brushKey = "SystemControlForegroundBaseMediumBrush"; tip = "No file open"; }
        else if (isDirty == true)   { brushKey = "DangerBrush";  tip = "Unsaved changes"; }
        else                        { brushKey = "SuccessBrush"; tip = "Saved"; }

        ToolTip.SetTip(_dirtyLabel, tip);
        if (this.TryFindResource(brushKey, out var brush) && brush is IBrush b)
            _dirtyLabel.Foreground = b;
    }
}
