namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListPage : Page, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private IFilters? _filters;
    private IGridProperties? _gridProperties;

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

    public IFilters? Filters
    {
        get => _filters;
        set
        {
            _filters = value;
            OnPropertyChanged(nameof(Filters));
        }
    }

    public IGridProperties? GridProperties
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
