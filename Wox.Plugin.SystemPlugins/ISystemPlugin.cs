using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.SystemPlugins
{
    public interface ISystemPlugin : IPlugin
    {
        string ID { get; }   
        string Name { get; }
        string Description { get; }
    }
}
