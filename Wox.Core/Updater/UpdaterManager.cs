using Squirrel;

namespace Wox.Core.Updater
{
    public class UpdaterManager
    {
        private static UpdaterManager instance;

        public static UpdaterManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UpdaterManager();
                }
                return instance;
            }
        }

        private UpdaterManager() { }

        public void CheckUpdate()
        {
            using (var mgr = new UpdateManager("https://path/to/my/update/folder", "nuget-package-id", FrameworkVersion.Net45))
            {
                 mgr.UpdateApp();
            }
        }
    }
}
