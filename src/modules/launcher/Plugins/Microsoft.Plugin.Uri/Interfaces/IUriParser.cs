namespace Microsoft.Plugin.Uri.Interfaces
{
    public interface IUriParser
    {
        bool TryParse(string input, out System.Uri result);
    }
}
