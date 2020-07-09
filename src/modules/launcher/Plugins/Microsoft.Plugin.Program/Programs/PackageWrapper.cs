using Package = Windows.ApplicationModel.Package;

namespace Microsoft.Plugin.Program.Programs
{
    public class PackageWrapper : IPackage
    {
        public string Name { get; }

        public string FullName { get; }

        public string FamilyName { get; }

        public string InstalledLocation { get; }

        public bool IsFramework { get; }

        public PackageWrapper(string Name, string FullName, string FamilyName, string InstalledLocation, bool IsFramework)
        {
            this.Name = Name;
            this.FullName = FullName;
            this.FamilyName = FamilyName;
            this.InstalledLocation = InstalledLocation;
            this.IsFramework = IsFramework;
        }

        public static PackageWrapper GetWrapperFromPackage(Package package)
        {
            return new PackageWrapper(
                        package.Id.Name,
                        package.Id.FullName,
                        package.Id.FamilyName,
                        package.InstalledLocation.Path,
                        package.IsFramework);
        }
    }
}
