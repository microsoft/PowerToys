using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Control = System.Windows.Controls.Control;

namespace Wox.Plugin.SystemPlugins.Sys
{
    public class Sys : BaseSystemPlugin, ISettingProvider 
	{
		List<Result> availableResults = new List<Result>();

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

		#region Overrides

		public override string ID
		{
            get { return "CEA08895D2544B019B2E9C5009600DF4"; }
        }

        public override string Name
        {
            get { return "System Commands"; }
        }

        public override string IcoPath
        {
            get { return @"Images\lock.png"; }
        }

        public override string Description
        {
            get { return "Provide System related commands. e.g. shutdown,lock,setting etc."; }
        }

		#endregion

        public Control CreateSettingPanel()
        {
            return new SysSettings(availableResults);
        }

        protected override List<Result> QueryInternal(Query query)
        {
            if (query.RawQuery.EndsWith(" ") || query.RawQuery.Length <= 1) return new List<Result>();

            List<Result> results = new List<Result>();

            foreach (Result availableResult in availableResults)
            {
                if (availableResult.Title.ToLower().StartsWith(query.RawQuery.ToLower()))
                {
                    results.Add(availableResult);
                }
            }
            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
			availableResults.AddRange(new Result[] { 
				new Result
				{
					Title = "Shutdown",
					SubTitle = "Shutdown Computer",
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
				    SubTitle = "Log off current user",
				    Score = 100,
				    IcoPath = "Images\\logoff.png",
				    Action = (c) => ExitWindowsEx(EWX_LOGOFF, 0)
				},
				new Result
				{
				    Title = "Lock",
				    SubTitle = "Lock this computer",
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
                    Title = "Sleep",
                    SubTitle = "Put computer to sleep",
                    Score = 100,
                    IcoPath = "Images\\sleep.png",
                    Action = (c) => Application.SetSuspendState(PowerState.Suspend, false, false)
                },
                new Result
				{
				    Title = "Exit",
				    SubTitle = "Close this app",
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
				    SubTitle = "Restart Wox",
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
				    SubTitle = "Tweak this app",
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



    }
}
