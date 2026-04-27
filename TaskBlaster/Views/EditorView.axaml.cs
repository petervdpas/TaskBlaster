using System;
using System.IO;
using System.Reflection;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using System.Collections.Generic;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace TaskBlaster.Views;

public partial class EditorView : UserControl
{
    private readonly TextEditor _editor;
    private FoldingManager? _foldingManager;
    private readonly BraceFoldingStrategy _foldingStrategy = new();
    private RegistryOptions? _tmRegistry;
    private TextMate.Installation? _tmInstallation;
    private string _highlighter = HighlighterNative;

    public const string HighlighterTextMate = "TextMate";
    public const string HighlighterNative   = "Native";

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

        _editor.Document.TextChanged += OnDocumentTextChanged;
        _editor.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);
    }

    /// <summary>
    /// Turn the folding margin on or off. Switches cleanly on the fly so
    /// the Settings dialog can flip it without restarting the editor.
    /// </summary>
    public void SetCodeFoldingEnabled(bool enabled)
    {
        if (enabled && _foldingManager is null)
        {
            _foldingManager = FoldingManager.Install(_editor.TextArea);
            UpdateFoldings();
        }
        else if (!enabled && _foldingManager is not null)
        {
            FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }
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
        UpdateFoldings();
        if (IsDirty) return;
        IsDirty = true;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFoldings()
    {
        if (_foldingManager is null || _editor.Document is null) return;
        _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document);
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

    /// <summary>
    /// Pick the syntax highlighter backend. <see cref="HighlighterTextMate"/>
    /// uses TextMateSharp + the VS Code DarkPlus/LightPlus themes (richer
    /// colours, heavier on scroll). <see cref="HighlighterNative"/> uses
    /// AvaloniaEdit's built-in xshd C# definition (lighter, snappier).
    /// Switches cleanly on the fly so the Settings dialog can flip it
    /// without restarting the editor.
    /// </summary>
    public void SetHighlighter(string mode)
    {
        var normalized = string.Equals(mode, HighlighterNative, StringComparison.OrdinalIgnoreCase)
            ? HighlighterNative
            : HighlighterTextMate;
        if (string.Equals(_highlighter, normalized, StringComparison.Ordinal)
            && (normalized == HighlighterTextMate ? _tmInstallation is not null : _editor.SyntaxHighlighting is not null))
            return;

        _highlighter = normalized;

        if (normalized == HighlighterTextMate)
        {
            _editor.SyntaxHighlighting = null;
            InstallTextMate(CurrentTmTheme());
        }
        else
        {
            DisposeTextMate();
            _editor.SyntaxHighlighting = LoadNativeHighlighting(ActualThemeVariant);
        }
    }

    public void ApplyTheme(ThemeVariant variant)
    {
        if (_highlighter == HighlighterTextMate)
        {
            if (_tmInstallation is null || _tmRegistry is null) return;
            var tmTheme = variant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
            _tmInstallation.SetTheme(_tmRegistry.LoadTheme(tmTheme));
        }
        else
        {
            // Native mode ships separate xshd files for dark and light so the
            // colour palette flips with the app theme instead of staying stuck
            // on whichever variant was active when the editor opened.
            _editor.SyntaxHighlighting = LoadNativeHighlighting(variant);
        }
    }

    private static IHighlightingDefinition LoadNativeHighlighting(ThemeVariant variant)
    {
        var resourceName = variant == ThemeVariant.Dark
            ? "TaskBlaster.Resources.Highlighting.CSharp.Dark.xshd"
            : "TaskBlaster.Resources.Highlighting.CSharp.Light.xshd";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private ThemeName CurrentTmTheme()
        => ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;

    private void InstallTextMate(ThemeName theme)
    {
        DisposeTextMate();
        _tmRegistry = new RegistryOptions(theme);
        _tmInstallation = _editor.InstallTextMate(_tmRegistry);
        _tmInstallation.SetGrammar(_tmRegistry.GetScopeByLanguageId("csharp"));
    }

    private void DisposeTextMate()
    {
        _tmInstallation?.Dispose();
        _tmInstallation = null;
        _tmRegistry = null;
    }

    private void SetTextInternal(string value)
    {
        var doc = _editor.Document!;
        doc.TextChanged -= OnDocumentTextChanged;
        doc.Text = value;
        doc.TextChanged += OnDocumentTextChanged;
        UpdateFoldings();
        MarkClean();
    }
}

/// <summary>
/// Brace-based folding strategy: any matching <c>{ … }</c> pair that spans
/// more than one line becomes a foldable region. Naive about strings and
/// comments — braces inside them are still counted, which can occasionally
/// produce odd foldings but never crashes.
/// </summary>
internal sealed class BraceFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateNewFoldings(document);
        manager.UpdateFoldings(foldings, firstErrorOffset: -1);
    }

    private static IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var startOffsets = new Stack<int>();
        var lastNewLineOffset = 0;
        for (var i = 0; i < document.TextLength; i++)
        {
            var c = document.GetCharAt(i);
            if (c == '{')
            {
                startOffsets.Push(i);
            }
            else if (c == '}' && startOffsets.Count > 0)
            {
                var startOffset = startOffsets.Pop();
                // Only fold spans that cross a newline; single-line { ... }
                // would just collapse to the same row and confuse the user.
                if (startOffset < lastNewLineOffset)
                    foldings.Add(new NewFolding(startOffset, i + 1));
            }
            else if (c == '\n' || c == '\r')
            {
                lastNewLineOffset = i + 1;
            }
        }
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
