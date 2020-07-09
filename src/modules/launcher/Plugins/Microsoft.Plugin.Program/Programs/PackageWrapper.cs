using Package = Windows.ApplicationModel.Package;

namespace Microsoft.Plugin.Program.Programs
{
    public class PackageWrapper : IPackage
    {
        public string Name { get; }

        public string FullName { get; }

        public string FamilyName { get; }

        public bool IsFramework { get; }

        public bool IsDevelopmentMode { get; }

        public string InstalledLocation { get; }

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
            return new PackageWrapper(
                        package.Id.Name,
                        package.Id.FullName,
                        package.Id.FamilyName,
                        package.IsFramework, 
                        package.IsDevelopmentMode,
                        package.InstalledLocation.Path
                        );
        }
    }
}
