namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListSection : ISection
{
    public string Title { get; set; } = "";
    public virtual IListItem[] Items { get; set; } = [];
}
