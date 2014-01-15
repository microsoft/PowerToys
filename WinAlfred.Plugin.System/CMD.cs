using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinAlfred.Plugin.System
{
    public class CMD : BaseSystemPlugin
    {
        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.RawQuery.StartsWith(">") && query.RawQuery.Length > 1)
            {
                string cmd = query.RawQuery.Substring(1);
                Result result = new Result
                {
                    Title = cmd,
                    SubTitle = "execute command through command shell" ,
                    IcoPath = "Images/cmd.png",
                    Action = () =>
                    {
                        Process process = new Process();
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            WindowStyle = ProcessWindowStyle.Normal,
                            FileName = "cmd.exe",
                            Arguments = "/C " + cmd
                        };
                        process.StartInfo = startInfo;
                        process.Start();
                    }
                };
                results.Add(result);
            }
            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
        }

    }
}
