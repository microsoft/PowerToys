namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListItem : BaseObservable, IListItem
{
    protected string _Title = "";
    protected string _Subtitle = "";
    protected ITag[] _Tags = [];
    protected IDetails? _Details;
    protected ICommand _Command;
    protected IContextItem[] _MoreCommands = [];
    protected IFallbackHandler? _FallbackHandler;

    public string Title { get => !string.IsNullOrEmpty(this._Title) ? this._Title : _Command.Name; set { _Title = value; OnPropertyChanged(nameof(Title)); } }
    public string Subtitle { get => _Subtitle; set { _Subtitle = value; OnPropertyChanged(nameof(Subtitle)); } }
    public ITag[] Tags { get => _Tags; set { _Tags = value; OnPropertyChanged(nameof(Tags)); } }
    public IDetails? Details { get => _Details; set { _Details = value; OnPropertyChanged(nameof(Details)); } }
    public ICommand Command { get => _Command; set { _Command = value; OnPropertyChanged(nameof(Command)); } }
    public IContextItem[] MoreCommands { get => _MoreCommands; set { _MoreCommands = value; OnPropertyChanged(nameof(MoreCommands)); } }

    public IFallbackHandler? FallbackHandler { get => _FallbackHandler ?? _Command as IFallbackHandler; init { _FallbackHandler = value; } }

    public ListItem(ICommand command)
    {
        _Command = command;
        _Title = command.Name;
    }
}
