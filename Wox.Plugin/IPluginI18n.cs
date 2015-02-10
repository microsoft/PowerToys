using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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