namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class DetailsElement : IDetailsElement
{
    public string Key { get; set; }
    public IDetailsData? Data { get; set; }
}
