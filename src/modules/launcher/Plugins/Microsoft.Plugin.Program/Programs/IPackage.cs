using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Plugin.Program.Programs
{
    public interface IPackage
    {
        string Name { get; }

        string FullName { get; }

        string FamilyName { get; }

        bool IsFramework { get; }

        bool IsDevelopmentMode { get; }

        string InstalledLocation { get; }
    }
}
