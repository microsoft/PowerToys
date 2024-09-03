namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class MarkdownPage : Action, IMarkdownPage
{
    private bool _Loading = false;
    protected ITag[] _Tags = [];
    protected IDetails? _Details = null;
    protected string _Title = "";

    public string Title { get => !string.IsNullOrEmpty(this._Title) ? this._Title : this.Name; set { _Title = value; OnPropertyChanged(nameof(Title)); } }
    public bool Loading { get => _Loading; set { _Loading = value; OnPropertyChanged(nameof(Loading)); } }
    public ITag[] Tags { get => _Tags; set { _Tags = value; OnPropertyChanged(nameof(Tags)); } }
    // public IDetails Details { get => _Details; set { _Details = value; OnPropertyChanged(nameof(Details)); } }
    public IContextItem[] Commands { get; set; } = [];

    public virtual string[] Bodies() => throw new NotImplementedException();
    public virtual IDetails Details() => null;
}
