using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using NAppUpdate.Framework.Tasks;
using Newtonsoft.Json;
using Wox.Core.i18n;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Core.Updater
{
    public class UpdaterManager
    {
        private static UpdaterManager instance;
        private const string VersionCheckURL = "https://api.getwox.com/release/latest/";
        private const string UpdateFeedURL = "http://upgrade.getwox.com/update.xml";
        //private const string UpdateFeedURL = "http://127.0.0.1:8888/update.xml";
        private static SemanticVersion currentVersion;

        public event EventHandler PrepareUpdateReady;
        public event EventHandler UpdateError;

        public Release NewRelease { get; set; }

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

        private bool IsNewerThanCurrent(Release release)
        {
            if (release == null) return false;

            return new SemanticVersion(release.version) > CurrentVersion;
        }

        public List<string> GetAvailableUpdateFiles()
        {
            List<string> files = new List<string>();
            foreach (var task in UpdateManager.Instance.Tasks)
            {
                if (task is FileUpdateTask)
                {
                    files.Add(((FileUpdateTask)task).LocalPath);
                }
            }
            return files;
        }

        public void CheckUpdate()
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                string json = HttpRequest.Get(VersionCheckURL, HttpProxy.Instance);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        NewRelease = JsonConvert.DeserializeObject<Release>(json);
                        if (IsNewerThanCurrent(NewRelease) && !UserSettingStorage.Instance.DontPromptUpdateMsg)
                        {
                            StartUpdate();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e);
                    }
                }
            });
        }

        private void StartUpdate()
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
                    OnPrepareUpdateReady();
                }, null);
            }, null);
        }

        public void CleanUp()
        {
            UpdateManager.Instance.CleanUp();
        }

        public void ApplyUpdates()
        {
            // ApplyUpdates is a synchronous method by design. Make sure to save all user work before calling
            // it as it might restart your application
            // get out of the way so the console window isn't obstructed
            try
            {
                UpdateManager.Instance.ApplyUpdates(true, UserSettingStorage.Instance.EnableUpdateLog, false);
            }
            catch (System.Exception e)
            {
                string updateError = InternationalizationManager.Instance.GetTranslation("update_wox_update_error");
                Log.Error(e);
                MessageBox.Show(updateError);
                OnUpdateError();
            }

            UpdateManager.Instance.CleanUp();
        }

        private IUpdateSource GetUpdateSource()
        {
            var source = new WoxUpdateSource(UpdateFeedURL, HttpRequest.GetWebProxy(HttpProxy.Instance));
            return source;
        }

        protected virtual void OnPrepareUpdateReady()
        {
            var handler = PrepareUpdateReady;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected virtual void OnUpdateError()
        {
            var handler = UpdateError;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
