
using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;

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

        private UpdaterManager()
        {
            UpdateManager.Instance.UpdateSource = GetUpdateSource();
        }

        public void CheckUpdate()
        {
            // Get a local pointer to the UpdateManager instance
            UpdateManager updManager = UpdateManager.Instance;

            updManager.BeginCheckForUpdates(asyncResult =>
            {
                if (asyncResult.IsCompleted)
                {
                    // still need to check for caught exceptions if any and rethrow
                    ((UpdateProcessAsyncResult)asyncResult).EndInvoke();

                    // No updates were found, or an error has occured. We might want to check that...
                    if (updManager.UpdatesAvailable == 0)
                    {
                        MessageBox.Show("All is up to date!");
                        return;
                    }
                }

                updManager.BeginPrepareUpdates(result =>
                {
                    ((UpdateProcessAsyncResult)result).EndInvoke();

                    // ApplyUpdates is a synchronous method by design. Make sure to save all user work before calling
                    // it as it might restart your application
                    // get out of the way so the console window isn't obstructed
                    try
                    {
                        updManager.ApplyUpdates(true,false,true);
                    }
                    catch
                    {
                        // this.WindowState = WindowState.Normal;
                        MessageBox.Show(
                            "An error occurred while trying to install software updates");
                    }

                    updManager.CleanUp();
                }, null);
            }, null);
        }

        public void Reinstall()
        {
            UpdateManager.Instance.ReinstateIfRestarted();
        }

        private void OnPrepareUpdatesCompleted(bool obj)
        {
            UpdateManager updManager = UpdateManager.Instance;

            DialogResult dr = MessageBox.Show(
                    "Updates are ready to install. Do you wish to install them now?",
                    "Software updates ready",
                     MessageBoxButtons.YesNo);

            if (dr == DialogResult.Yes)
            {
                // This is a synchronous method by design, make sure to save all user work before calling
                // it as it might restart your application
                updManager.ApplyUpdates(true,true,true);
            }
        }

        private IUpdateSource GetUpdateSource()
        {
            // Normally this would be a web based source.
            // But for the demo app, we prepare an in-memory source.
            var source = new NAppUpdate.Framework.Sources.SimpleWebSource("http://127.0.0.1:8888/Update.xml");
            return source;
        }
    }
}
