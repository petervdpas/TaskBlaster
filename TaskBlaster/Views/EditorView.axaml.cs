using System.IO;
using Avalonia.Controls;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace TaskBlaster.Views;

public partial class EditorView : UserControl
{
    private readonly TextEditor _editor;
    private readonly RegistryOptions _tmRegistry;
    private readonly TextMate.Installation _tmInstallation;

    public EditorView()
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;

        _tmRegistry = new RegistryOptions(ThemeName.DarkPlus);
        _tmInstallation = _editor.InstallTextMate(_tmRegistry);
        _tmInstallation.SetGrammar(_tmRegistry.GetScopeByLanguageId("csharp"));
    }

    public string Text
    {
        get => _editor.Text;
        set => _editor.Text = value;
    }

    public void LoadFile(string path)
    {
        if (!File.Exists(path)) return;
        _editor.Text = File.ReadAllText(path);
    }

    public void ApplyTheme(ThemeVariant variant)
    {
        var tmTheme = variant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
        _tmInstallation.SetTheme(_tmRegistry.LoadTheme(tmTheme));
    }
}
