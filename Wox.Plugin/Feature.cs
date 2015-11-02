using System.Collections.Generic;

namespace Wox.Plugin
{
    public interface IFeatures { }

    public interface IContextMenu : IFeatures
    {
        List<Result> LoadContextMenus(Result selectedResult);
    }

    public interface IExclusiveQuery : IFeatures
    {
        bool IsExclusiveQuery(Query query);
    }

    /// <summary>
    /// Represent plugin query will be executed in UI thread directly. Don't do long-running operation in Query method if you implement this interface
    /// <remarks>This will improve the performance of instant search like websearch or cmd plugin</remarks>
    /// </summary>
    public interface IInstantQuery : IFeatures
    {
        bool IsInstantQuery(string query);
    }

    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        string GetLanguagesFolder();

        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();
    }
}
