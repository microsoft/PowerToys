namespace Microsoft.Windows.CommandPalette.Extensions.Helpers;

public class GoToPageArgs : IGoToPageArgs
{
    public required string PageId { get; set; }
}
