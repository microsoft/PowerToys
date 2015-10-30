namespace Wox.Plugin
{
    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n
    {
        string GetLanguagesFolder();

        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();
    }
}