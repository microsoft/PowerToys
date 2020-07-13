using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Program.Programs
{
    public interface IShellLinkHelper
    {
        string retrieveTargetPath(string path);
        string description { get; set; }
        string Arguments { get; set; }
        bool hasArguments { get; set; }

    }
}
