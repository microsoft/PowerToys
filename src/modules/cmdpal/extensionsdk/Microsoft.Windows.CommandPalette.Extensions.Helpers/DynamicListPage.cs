namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class DynamicListPage : ListPage, IDynamicListPage
{
    public virtual ISection[] GetItems(string query) => throw new NotImplementedException();
}
