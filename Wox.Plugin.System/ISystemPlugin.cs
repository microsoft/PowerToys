using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.System
{
    public interface ISystemPlugin : IPlugin
    {
        string Name { get; }
        string Description { get; }
    }
}
