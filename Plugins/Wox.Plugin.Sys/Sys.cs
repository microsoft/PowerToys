using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Wox.Plugin.Sys
{
    public class Sys : IPlugin, ISettingProvider ,IPluginI18n
	{
		List<Result> availableResults = new List<Result>();
        private PluginInitContext context;

		#region DllImport
		
        internal const int EWX_LOGOFF = 0x00000000;
        internal const int EWX_SHUTDOWN = 0x00000001;
        internal const int EWX_REBOOT = 0x00000002;
        internal const int EWX_FORCE = 0x00000004;
        internal const int EWX_POWEROFF = 0x00000008;
        internal const int EWX_FORCEIFHUNG = 0x00000010;
        [DllImport("user32")]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32")]
        private static extern void LockWorkStation();

		#endregion

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new SysSettings(availableResults);
        }

        public List<Result> Query(Query query)
        {
            if (availableResults.Count == 0)
            {
                LoadCommands();
            }

            List<Result> results = new List<Result>();
            foreach (Result availableResult in availableResults)
            {
                if (availableResult.Title.ToLower().StartsWith(query.Search.ToLower()))
                {
                    results.Add(availableResult);
                }
            }
            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        private void LoadCommands()
        {
            availableResults.AddRange(new Result[] { 
				new Result
				{
					Title = "Shutdown",
					SubTitle = context.API.GetTranslation("wox_plugin_sys_shutdown_computer"),
					Score = 100,
					IcoPath = "Images\\exit.png",
					Action = (c) =>
					{
						if (MessageBox.Show("Are you sure you want to shut the computer down?","Shutdown Computer?",MessageBoxButtons.YesNo,MessageBoxIcon.Warning) == DialogResult.Yes) {
							Process.Start("shutdown", "/s /t 0");
						}
					    return true;
					}
				},
				new Result
				{
				    Title = "Log off",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_log_off"),
				    Score = 100,
				    IcoPath = "Images\\logoff.png",
				    Action = (c) => ExitWindowsEx(EWX_LOGOFF, 0)
				},
				new Result
				{
				    Title = "Lock",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_lock"),
				    Score = 100,
				    IcoPath = "Images\\lock.png",
				    Action = (c) =>
				    {
				        LockWorkStation();
				        return true;
				    }
				},
				new Result
				{
				    Title = "Exit",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_exit"),
				    Score = 110,
				    IcoPath = "Images\\app.png",
				    Action = (c) =>
				    {
				        context.API.CloseApp();
				        return true;
				    }
				},
				new Result
				{
				    Title = "Restart Wox",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_restart"),
				    Score = 110,
				    IcoPath = "Images\\restart.png",
				    Action = (c) =>
				    {
				        ProcessStartInfo Info = new ProcessStartInfo();
				        Info.Arguments = "/C ping 127.0.0.1 -n 1 && \"" + Application.ExecutablePath + "\"";
				        Info.WindowStyle = ProcessWindowStyle.Hidden;
				        Info.CreateNoWindow = true;
				        Info.FileName = "cmd.exe";
				        Process.Start(Info);
				        context.API.CloseApp();
				        return true;
				    }
				},
				new Result
				{
				    Title = "Settings",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_setting"),
				    Score = 100,
				    IcoPath = "Images\\app.png",
				    Action = (c) =>
				    {
				        context.API.OpenSettingDialog();
				        return true;
				    }
				}
			});
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");

        }
    }
}
