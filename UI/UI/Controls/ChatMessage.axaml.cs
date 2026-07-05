using Avalonia;
using Avalonia.Controls;

using Transmitter.Comms;

namespace UI.Controls;

public partial class ChatMessage : UserControl
{
    public static readonly StyledProperty<bool> IsMyMessageProperty = AvaloniaProperty.Register<ChatMessage, bool>(nameof(IsMyMessage));
    public bool IsMyMessage
    {
        get => GetValue(IsMyMessageProperty);
        set => SetValue(IsMyMessageProperty, value);
    }

    public static readonly StyledProperty<bool> CollapseInfoProperty = AvaloniaProperty.Register<ChatMessage, bool>(nameof(CollapseInfo));
    public bool CollapseInfo
    {
        get => GetValue(CollapseInfoProperty);
        set => SetValue(CollapseInfoProperty, value);
    }

    public static readonly DirectProperty<ChatMessage, Mail> MailProperty = AvaloniaProperty.RegisterDirect<ChatMessage, Mail>(nameof(Mail), o => o.Mail);
    public Mail Mail { get; }

    public ChatMessage(Mail mail)
    {
        InitializeComponent();
        Mail = mail;
    }
}