
using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Threading;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using Wox.Core.i18n;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;

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

        public bool IsUpdateAvailable()
        {
            return UpdateManager.Instance.UpdatesAvailable > 0;
        }

        public void CheckUpdate()
        {
            UpdateManager updManager = UpdateManager.Instance;
            updManager.BeginCheckForUpdates(asyncResult =>
            {
                if (asyncResult.IsCompleted)
                {
                    // still need to check for caught exceptions if any and rethrow
                    try
                    {
                        ((UpdateProcessAsyncResult)asyncResult).EndInvoke();
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e);
                        updManager.CleanUp();
                        return;
                    }

                    // No updates were found, or an error has occured. We might want to check that...
                    if (updManager.UpdatesAvailable == 0)
                    {
                        return;
                    }
                }

                updManager.BeginPrepareUpdates(result =>
                {
                    ((UpdateProcessAsyncResult)result).EndInvoke();
                    string updateReady = InternationalizationManager.Instance.GetTranslation("update_wox_update_ready");
                    string updateInstall = InternationalizationManager.Instance.GetTranslation("update_wox_update_install");
                    updateInstall = string.Format(updateInstall, updManager.UpdatesAvailable);
                    DialogResult dr = MessageBox.Show(updateInstall, updateReady, MessageBoxButtons.YesNo);

                    if (dr == DialogResult.Yes)
                    {

                        // ApplyUpdates is a synchronous method by design. Make sure to save all user work before calling
                        // it as it might restart your application
                        // get out of the way so the console window isn't obstructed
                        try
                        {
                            updManager.ApplyUpdates(true, UserSettingStorage.Instance.EnableUpdateLog, false);
                        }
                        catch (System.Exception e)
                        {
                            string updateError =
                                InternationalizationManager.Instance.GetTranslation("update_wox_update_error");
                            Log.Error(e);
                            MessageBox.Show(updateError);
                        }

                        updManager.CleanUp();
                    }
                    else
                    {
                        updManager.CleanUp();
                    }
                }, null);
            }, null);
        }

        private IUpdateSource GetUpdateSource()
        {
            // Normally this would be a web based source.
            // But for the demo app, we prepare an in-memory source.
            var source = new SimpleWebSource("http://127.0.0.1:8888/Update.xml");
            return source;
        }
    }
}
