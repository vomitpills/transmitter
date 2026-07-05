using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;

namespace UI.Controls;

public partial class ChatHistory : UserControl
{
    public static readonly DirectProperty<ChatHistory, ObservableCollection<ChatMessage>> ItemsProperty = AvaloniaProperty.RegisterDirect<ChatHistory, ObservableCollection<ChatMessage>>(nameof(Items), o => o.Items);
    public ObservableCollection<ChatMessage> Items { get; } = [];

    public ChatHistory()
    {
        InitializeComponent();
    }
}