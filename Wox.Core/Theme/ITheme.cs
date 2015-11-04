using System.Collections.Generic;

namespace Wox.Core.Theme
{
    interface ITheme
    {
        void ChangeTheme(string themeName);
        List<string> LoadAvailableThemes();
    }
}
