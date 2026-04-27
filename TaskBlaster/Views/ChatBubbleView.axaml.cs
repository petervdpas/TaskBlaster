using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TaskBlaster.Views;

/// <summary>
/// One chat-history bubble. Just the markdown body wrapped in a themed
/// Border. Copy lives in the chat panel header (where it's always visible
/// regardless of bubble height / scroll position) — chat bubble doesn't
/// own a copy button anymore.
/// </summary>
public partial class ChatBubbleView : UserControl
{
    private readonly Border _bubble;
    private readonly ContentControl _bodyHost;

    public ChatBubbleView()
    {
        InitializeComponent();
        _bubble   = this.FindControl<Border>("BubbleBorder")!;
        _bodyHost = this.FindControl<ContentControl>("BodyHost")!;
    }

    /// <summary>Configure the bubble. <paramref name="userAccentBrush"/> + <paramref name="userForeground"/> are only used for the user variant.</summary>
    public void SetContent(
        Control body,
        bool isUser,
        IBrush? userAccentBrush,
        IBrush? userForeground)
    {
        _bodyHost.Content = body;

        if (isUser)
        {
            HorizontalAlignment = HorizontalAlignment.Right;
            MaxWidth = 320;
            if (userAccentBrush is not null) _bubble.Background = userAccentBrush;
            _bubble.BorderThickness = new Thickness(0);
            if (userForeground is not null) Foreground = userForeground;
        }
        else
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            // Assistant keeps its XAML-bound DynamicResource Background +
            // BorderBrush — repaints on theme switch automatically.
        }
    }
}
