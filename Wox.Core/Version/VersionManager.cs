using System.Reflection;

namespace Wox.Core.Version
{
    public class VersionManager
    {
        private static VersionManager versionManager;
        private static SemanticVersion currentVersion;

        public static VersionManager Instance
        {
            get
            {
                if (versionManager == null)
                {
                    versionManager = new VersionManager();
                }
                return versionManager;
            }
        }

        private VersionManager() { }

        public SemanticVersion CurrentVersion
        {
            get
            {
                if (currentVersion == null)
                {
                    currentVersion = new SemanticVersion(Assembly.GetExecutingAssembly().GetName().Version);
                }
                return currentVersion;
            }
        }
    }
}
