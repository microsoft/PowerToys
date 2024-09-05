namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListPage : Action, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private bool _loading;
    private IFilters _filters;
    private IGridProperties _gridProperties;

    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public bool ShowDetails
    {
        get => _showDetails;
        set
        {
            _showDetails = value;
            OnPropertyChanged(nameof(ShowDetails));
        }
    }

    public bool Loading
    {
        get => _loading;
        set
        {
            _loading = value;
            OnPropertyChanged(nameof(Loading));
        }
    }

    public IFilters Filters
    {
        get => _filters;
        set
        {
            _filters = value;
            OnPropertyChanged(nameof(Filters));
        }
    }

    public IGridProperties GridProperties
    {
        get => _gridProperties;
        set
        {
            _gridProperties = value;
            OnPropertyChanged(nameof(GridProperties));
        }
    }

    public virtual ISection[] GetItems() => throw new NotImplementedException();
}
