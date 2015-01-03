using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Core.Theme
{
    interface ITheme
    {
        void ChangeTheme(string themeName);
        List<string> LoadAvailableThemes();
    }
}
