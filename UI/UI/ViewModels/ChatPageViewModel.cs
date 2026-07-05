using System.Collections.ObjectModel;

using Transmitter.Doohickeys;

using UI.Controls;

namespace UI.ViewModels;

public partial class ChatPageViewModel : ViewModelBase
{
    public ObservableCollection<ChatMessage> ChatMessages { get; set; }

    public ChatPageViewModel()
    {

        GibberishApparatus.CrankALil();
    }
}