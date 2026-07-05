using Transmitter.Comms;

namespace UI.ViewModels;

public partial class ChatMessageViewModel : ViewModelBase
{
    public Profile SenderProfile { get; } = new("test", 0);
}
