using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Plugin.Features;

namespace Wox.Plugin
{
    public interface IInstantSearch
    {
        bool IsInstantSearch(string query);
    }
}
