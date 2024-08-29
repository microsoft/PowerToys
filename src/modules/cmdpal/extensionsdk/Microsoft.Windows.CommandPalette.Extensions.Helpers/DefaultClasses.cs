using Windows.Foundation;
using Windows.UI;

namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

// TODO! We probably want to have OnPropertyChanged raise the event
// asynchonously, so as to not block the extension app while it's being
// processed in the host app.
public class BaseObservable : INotifyPropChanged
{
    public event TypedEventHandler<object, PropChangedEventArgs>? PropChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        if (PropChanged != null)
            PropChanged.Invoke(this, new Microsoft.Windows.CommandPalette.Extensions.PropChangedEventArgs(propertyName));
    }
}

public class Action : BaseObservable, ICommand
{
    protected string _Name = "";
    protected IconDataType _Icon = new("");
    public string Name { get => _Name; set { _Name = value; OnPropertyChanged(nameof(Name)); } }
    public IconDataType Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(nameof(Icon)); } }
}

public class InvokableCommand : Action, IInvokableCommand
{
    public virtual ICommandResult Invoke() => throw new NotImplementedException();
}

public class NoOpAction : InvokableCommand
{
    public override ICommandResult Invoke() => ActionResult.KeepOpen();
}

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

public class Tag : BaseObservable, ITag
{
    protected Color _Color = new();
    protected IconDataType _Icon = null;
    protected string _Text = "";
    protected string _ToolTip = "";
    protected ICommand _Action;

    public Color Color { get => _Color; set { _Color = value; OnPropertyChanged(nameof(Color)); } }
    public IconDataType Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(nameof(Icon)); } }
    public string Text { get => _Text; set { _Text = value; OnPropertyChanged(nameof(Text)); } }
    public string ToolTip { get => _ToolTip; set { _ToolTip = value; OnPropertyChanged(nameof(ToolTip)); } }
    public ICommand Command { get => _Action; set { _Action = value; OnPropertyChanged(nameof(Action)); } }

}

public class ListSection : ISection
{
    public string Title { get; set; } = "";
    public virtual IListItem[] Items { get; set; } = [];
}

public class CommandContextItem : ICommandContextItem
{
    public bool IsCritical { get; set; }
    public ICommand Command { get; set; }
    public string Tooltip { get; set; } = "";
    public CommandContextItem(ICommand command)
    {
        Command = command;
    }
}

public class ActionResult : ICommandResult
{
    private ICommandResultArgs _Args = null;
    private CommandResultKind _Kind = CommandResultKind.Dismiss;
    public ICommandResultArgs Args => _Args;
    public CommandResultKind Kind => _Kind;
    public static ActionResult Dismiss() {
        return new ActionResult() { _Kind = CommandResultKind.Dismiss };
    }
    public static ActionResult GoHome()
    {
        return new ActionResult() { _Kind = CommandResultKind.GoHome };
    }
    public static ActionResult KeepOpen()
    {
        return new ActionResult() { _Kind = CommandResultKind.KeepOpen };
    }
}

public class GoToPageArgs : IGoToPageArgs
{
    public required string PageId { get; set; }
}

public class SeparatorContextItem : ISeparatorContextItem
{
}

public class SeparatorFilterItem : ISeparatorFilterItem
{
}

public class Filter : IFilter
{
    public IconDataType Icon => throw new NotImplementedException();
    public string Id => throw new NotImplementedException();
    public string Name => throw new NotImplementedException();
}

public class ListPage : Action, IListPage
{
    private string _PlaceholderText = "";
    private string _SearchText = "";
    private bool _ShowDetails = false;
    private bool _Loading = false;
    private IFilters _Filters = null;
    private IGridProperties _GridProperties = null;

    public string PlaceholderText { get => _PlaceholderText; set { _PlaceholderText = value; OnPropertyChanged(nameof(PlaceholderText)); } }
    public string SearchText { get => _SearchText; set { _SearchText = value; OnPropertyChanged(nameof(SearchText)); } }
    public bool ShowDetails { get => _ShowDetails; set { _ShowDetails = value; OnPropertyChanged(nameof(ShowDetails)); } }
    public bool Loading { get => _Loading; set { _Loading = value; OnPropertyChanged(nameof(Loading)); } }
    public IFilters Filters { get => _Filters; set { _Filters = value; OnPropertyChanged(nameof(Filters)); } }
    public IGridProperties GridProperties { get => _GridProperties; set { _GridProperties = value; OnPropertyChanged(nameof(GridProperties)); } }

    public virtual ISection[] GetItems() => throw new NotImplementedException();
}

public class DynamicListPage : ListPage, IDynamicListPage
{
    public virtual ISection[] GetItems(string query) => throw new NotImplementedException();
}

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

public class Form: IForm
{
    public string Data { get; set; }
    public string State { get; set; }
    public string Template { get; set; }

    public virtual string DataJson() => Data;
    public virtual string StateJson() => State;
    public virtual string TemplateJson() => Template;
    public virtual ICommandResult SubmitForm(string payload) => throw new NotImplementedException();
}
public class FormPage : Action, IFormPage
{
    private bool _Loading = false;

    public bool Loading { get => _Loading; set { _Loading = value; OnPropertyChanged(nameof(Loading)); } }

    public virtual IForm[] Forms() => throw new NotImplementedException();
}

public class DetailsTags : IDetailsTags
{
    public ITag[] Tags { get; set; }
}

public class DetailsLink : IDetailsLink
{
    public Uri Link { get; set; }
    public string Text { get; set; }
}

public class DetailsSeparator : IDetailsSeparator
{
}

public class Details : BaseObservable, IDetails
{
    protected IconDataType _HeroImage;
    protected string _Title;
    protected string _Body;
    protected IDetailsElement[] _Metadata = [];

    public IconDataType HeroImage { get => _HeroImage; set { _HeroImage = value; OnPropertyChanged(nameof(HeroImage)); } }
    public string Title { get => _Title; set { _Title = value; OnPropertyChanged(nameof(Title)); } }
    public string Body { get => _Body; set { _Body = value; OnPropertyChanged(nameof(Body)); } }
    public IDetailsElement[] Metadata { get => _Metadata; set { _Metadata = value; OnPropertyChanged(nameof(Metadata)); } }
}
public class DetailsElement : IDetailsElement
{
    public string Key { get; set; }
    public IDetailsData? Data { get; set; }
}
