using System;
using System.Collections.Generic;

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
    public interface IInstantQuery : IFeatures
    {
        [Obsolete("Empty interface is enough. it will be removed in v1.3.0 and possibly replaced by attribute")]
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

    public interface IMultipleActionKeywords
    {
        event ActionKeywordsChangedEventHandler ActionKeywordsChanged;
    }

    public class ActionKeywordsChangedEventArgs : EventArgs
    {
        public string OldActionKeyword { get; set; }
        public string NewActionKeyword { get; set; }
    }

    public delegate void ActionKeywordsChangedEventHandler(IMultipleActionKeywords sender, ActionKeywordsChangedEventArgs e);
}
