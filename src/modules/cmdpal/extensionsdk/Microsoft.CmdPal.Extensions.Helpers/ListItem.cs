namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListItem : BaseObservable, IListItem
{
    private string _title = "";
    private string _subtitle = "";
    private ITag[] _tags = [];
    private IDetails? _details;
    private ICommand _command;
    private IContextItem[] _moreCommands = [];
    private IFallbackHandler? _fallbackHandler;

    public string Title
    {
        get => !string.IsNullOrEmpty(this._title) ? this._title : _command.Name;
        set
        {
            _title = value; OnPropertyChanged(nameof(Title));
        }

    }

    public string Subtitle { get => _subtitle; set { _subtitle = value; OnPropertyChanged(nameof(Subtitle)); } }
    public ITag[] Tags { get => _tags; set { _tags = value; OnPropertyChanged(nameof(Tags)); } }
    public IDetails? Details { get => _details; set { _details = value; OnPropertyChanged(nameof(Details)); } }
    public ICommand Command { get => _command; set { _command = value; OnPropertyChanged(nameof(Command)); } }
    public IContextItem[] MoreCommands { get => _moreCommands; set { _moreCommands = value; OnPropertyChanged(nameof(MoreCommands)); } }

    public IFallbackHandler? FallbackHandler { get => _fallbackHandler ?? _command as IFallbackHandler; init { _fallbackHandler = value; } }

    public ListItem(ICommand command)
    {
        Command = command;
        Title = command.Name;
    }
}
