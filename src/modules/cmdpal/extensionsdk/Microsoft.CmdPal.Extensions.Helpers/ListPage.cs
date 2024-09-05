namespace Microsoft.CmdPal.Extensions.Helpers;

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
