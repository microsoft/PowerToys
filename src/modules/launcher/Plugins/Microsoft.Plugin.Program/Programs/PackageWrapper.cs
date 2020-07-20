using Microsoft.Plugin.Program.Logger;
using System.IO;
using Package = Windows.ApplicationModel.Package;

namespace Microsoft.Plugin.Program.Programs
{
    public class PackageWrapper : IPackage
    {
        public string Name { get; } = string.Empty;

        public string FullName { get; } = string.Empty;

        public string FamilyName { get; } = string.Empty;

        public bool IsFramework { get; } = false;

        public bool IsDevelopmentMode { get; } = false;

        public string InstalledLocation { get; } = string.Empty;

        public PackageWrapper() { }

        public PackageWrapper(string Name, string FullName, string FamilyName, bool IsFramework, bool IsDevelopmentMode, string InstalledLocation)
        {
            this.Name = Name;
            this.FullName = FullName;
            this.FamilyName = FamilyName;
            this.IsFramework = IsFramework;
            this.IsDevelopmentMode = IsDevelopmentMode;
            this.InstalledLocation = InstalledLocation;
        }

        public static PackageWrapper GetWrapperFromPackage(Package package)
        {
            try
            {
                return new PackageWrapper(
                        package.Id.Name,
                        package.Id.FullName,
                        package.Id.FamilyName,
                        package.IsFramework, 
                        package.IsDevelopmentMode,
                        package.InstalledLocation.Path
                        );
            }
            catch(FileNotFoundException ex)
            {
                ProgramLogger.LogException($"PackageWrapper", "GetWrapperFromPackage","package.InstalledLocation.Path",$"File Not Found for package {package.Id.Name}", ex);
                return new PackageWrapper();
            }
        }
    }
}
