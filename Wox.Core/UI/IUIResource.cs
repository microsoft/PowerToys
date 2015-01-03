using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Wox.Core.UI
{
    /// <summary>
    /// Object implement this interface will have the ability to has its own UI styles
    /// </summary>
    interface IUIResource
    {
        ResourceDictionary GetResourceDictionary();
    }
}
