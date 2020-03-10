using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Wox.Plugin
{
    public interface IFeatures { }

    public interface IContextMenu : IFeatures
    {
        List<Result> LoadContextMenus(Result selectedResult);
    }

    [Obsolete("If a plugin has a action keyword, then it is exclusive. This interface will be remove in v1.3.0")]
    public interface IExclusiveQuery : IFeatures
    {
        [Obsolete("If a plugin has a action keyword, then it is exclusive. This method will be remove in v1.3.0")]
        bool IsExclusiveQuery(Query query);
    }

    /// <summary>
    /// Represent plugin query will be executed in UI thread directly. Don't do long-running operation in Query method if you implement this interface
    /// <remarks>This will improve the performance of instant search like websearch or cmd plugin</remarks>
    /// </summary>
    [Obsolete("Wox is fast enough now, executed on ui thread is no longer needed")]
    public interface IInstantQuery : IFeatures
    {
        bool IsInstantQuery(string query);
    }

    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();
    }

    public interface IResultUpdated : IFeatures
    {
        event ResultUpdatedEventHandler ResultsUpdated;
    }

    public delegate void ResultUpdatedEventHandler(IResultUpdated sender, ResultUpdatedEventArgs e);

    public class ResultUpdatedEventArgs : EventArgs
    {
        public List<Result> Results;
        public Query Query;
    }
}
