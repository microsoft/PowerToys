using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinAlfred.Plugin.System
{
    public class Main : IPlugin
    {
        List<Result> availableResults = new List<Result>();

        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32")]
        public static extern void LockWorkStation();

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results = availableResults;
            }
            else
            {
                results.AddRange(availableResults.Where(result => result.Title.ToLower().Contains(query.ActionParameters[0].ToLower())));
            }
            return results;
        }

        public void Init()
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
    }
}
