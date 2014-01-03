using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinAlfred.Plugin.System
{
    public class Sys : ISystemPlugin
    {
        List<Result> availableResults = new List<Result>();

        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32")]
        public static extern void LockWorkStation();

        public List<Result> Query(Query query)
        {
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

        public void Init(PluginInitContext context)
        {
            availableResults.Add(new Result
            {
                Title = "Shutdown",
                SubTitle = "Shutdown Computer",
                Score = 100,
                Action = () => MessageBox.Show("shutdown")
            });
            availableResults.Add(new Result
            {
                Title = "Log off",
                SubTitle = "Log off current user",
                Score = 10,
                Action = () => MessageBox.Show("Logoff")
            });
            availableResults.Add(new Result
            {
                Title = "Lock",
                SubTitle = "Lock this computer",
                Score = 20,
                IcoPath = "Images\\lock.png",
                Action = () => LockWorkStation()
            });
        }

        public string Name
        {
            get
            {
                return "sys";
            }
        }

        public string Description
        {
            get
            {
                return "provide system commands";
            }
        }
    }
}
