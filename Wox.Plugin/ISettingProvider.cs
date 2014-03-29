using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace Wox.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();
    }
}
