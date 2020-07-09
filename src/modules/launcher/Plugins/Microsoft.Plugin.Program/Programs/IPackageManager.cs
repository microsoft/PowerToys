using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Program.Programs
{
    public interface IPackageManager
    {
        IEnumerable<IPackage> FindPackagesForCurrentUser();
    }
}
