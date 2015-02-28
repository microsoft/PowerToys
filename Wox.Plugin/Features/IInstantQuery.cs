namespace Wox.Plugin.Features
{
    /// <summary>
    /// Represent plugin query will be executed in UI thread directly. Don't do long-running operation in Query method if you implement this interface
    /// <remarks>This will improve the performance of instant search like websearch or cmd plugin</remarks>
    /// </summary>
    public interface IInstantQuery
    {
        bool IsInstantQuery(string query);
    }
}