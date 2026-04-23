using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace TaskBlaster.Views;

public partial class EditorView : UserControl
{
    private readonly TextEditor _editor;
    private readonly RegistryOptions _tmRegistry;
    private readonly TextMate.Installation _tmInstallation;

    public const double DefaultFontSize = 13;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 36;

    public event EventHandler? DirtyChanged;
    public event EventHandler? FontSizeChanged;

    public bool IsDirty { get; private set; }

    public double EditorFontSize => _editor.FontSize;

    public void ZoomIn() => SetFontSize(_editor.FontSize + 1);
    public void ZoomOut() => SetFontSize(_editor.FontSize - 1);
    public void ResetZoom() => SetFontSize(DefaultFontSize);

    private void SetFontSize(double size)
    {
        var clamped = Math.Clamp(size, MinFontSize, MaxFontSize);
        if (Math.Abs(clamped - _editor.FontSize) < 0.01) return;
        _editor.FontSize = clamped;
        FontSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    public EditorView()
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;

        _editor.Document ??= new TextDocument();

        _tmRegistry = new RegistryOptions(ThemeName.DarkPlus);
        _tmInstallation = _editor.InstallTextMate(_tmRegistry);
        _tmInstallation.SetGrammar(_tmRegistry.GetScopeByLanguageId("csharp"));

        _editor.Document.TextChanged += OnDocumentTextChanged;
        _editor.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if (e.Delta.Y > 0) ZoomIn();
        else if (e.Delta.Y < 0) ZoomOut();
        e.Handled = true;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (IsDirty) return;
        IsDirty = true;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Text
    {
        get => _editor.Document?.Text ?? string.Empty;
        set => SetTextInternal(value);
    }

    public void LoadFile(string path)
    {
        if (!File.Exists(path)) return;
        SetTextInternal(File.ReadAllText(path));
    }

    public void SaveTo(string path)
    {
        File.WriteAllText(path, _editor.Document?.Text ?? string.Empty);
        MarkClean();
    }

    public void MarkClean()
    {
        if (!IsDirty) return;
        IsDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTheme(ThemeVariant variant)
    {
        var tmTheme = variant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
        _tmInstallation.SetTheme(_tmRegistry.LoadTheme(tmTheme));
    }

    private void SetTextInternal(string value)
    {
        var doc = _editor.Document!;
        doc.TextChanged -= OnDocumentTextChanged;
        doc.Text = value;
        doc.TextChanged += OnDocumentTextChanged;
        MarkClean();
    }
}
