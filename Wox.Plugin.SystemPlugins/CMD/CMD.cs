using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using Control = System.Windows.Controls.Control;

namespace Wox.Plugin.SystemPlugins.CMD
{
    public class CMD : BaseSystemPlugin, ISettingProvider
    {
        private PluginInitContext context;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            List<Result> pushedResults = new List<Result>();
            if (query.RawQuery == ">")
            {
                IEnumerable<Result> history = CMDStorage.Instance.CMDHistory.OrderByDescending(o => o.Value)
                 .Select(m => new Result
                 {
                     Title = m.Key,
                     SubTitle = "this command has been executed " + m.Value + " times",
                     IcoPath = "Images/cmd.png",
                     Action = (c) =>
                     {
                         ExecuteCmd(m.Key);
                         return true;
                     },
                     ContextMenu = GetContextMenus(m.Key)
                 }).Take(5);

                results.AddRange(history);
            }

            if (query.RawQuery.StartsWith(">") && query.RawQuery.Length > 1)
            {
                string cmd = query.RawQuery.Substring(1);
                Result result = new Result
                {
                    Title = cmd,
                    Score = 5000,
                    SubTitle = "execute command through command shell",
                    IcoPath = "Images/cmd.png",
                    Action = (c) =>
                    {
                        ExecuteCmd(cmd);
                        return true;
                    },
                    ContextMenu = GetContextMenus(cmd)
                };

                try
                {
                    if (File.Exists(cmd) || Directory.Exists(cmd))
                    {
                        result.IcoPath = cmd;
                    }
                }
                catch (Exception) { }

                context.API.PushResults(query, context.CurrentPluginMetadata, new List<Result>() { result });
                pushedResults.Add(result);

                IEnumerable<Result> history = CMDStorage.Instance.CMDHistory.Where(o => o.Key.Contains(cmd))
                    .OrderByDescending(o => o.Value)
                    .Select(m =>
                    {
                        if (m.Key == cmd)
                        {
                            result.SubTitle = "this command has been executed " + m.Value + " times";
                            return null;
                        }

                        var ret = new Result
                        {
                            Title = m.Key,
                            SubTitle = "this command has been executed " + m.Value + " times",
                            IcoPath = "Images/cmd.png",
                            Action = (c) =>
                            {
                                ExecuteCmd(m.Key);
                                return true;
                            },
                            ContextMenu = GetContextMenus(m.Key)
                        };
                        try
                        {
                            if (File.Exists(m.Key) || Directory.Exists(m.Key))
                            {
                                ret.IcoPath = m.Key;
                            }
                        }
                        catch (Exception) { }

                        return ret;
                    }).Where(o => o != null).Take(4);

                context.API.PushResults(query, context.CurrentPluginMetadata, history.ToList());
                pushedResults.AddRange(history);

                try
                {
                    string basedir = null;
                    string dir = null;
                    string excmd = Environment.ExpandEnvironmentVariables(cmd);
                    if (Directory.Exists(excmd) && (cmd.EndsWith("/") || cmd.EndsWith(@"\")))
                    {
                        basedir = excmd;
                        dir = cmd;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(excmd)))
                    {
                        basedir = Path.GetDirectoryName(excmd);
                        var dirn = Path.GetDirectoryName(cmd);
                        dir = (dirn.EndsWith("/") || dirn.EndsWith(@"\")) ? dirn : cmd.Substring(0, dirn.Length + 1);
                    }

                    if (basedir != null)
                    {
                        List<string> autocomplete = Directory.GetFileSystemEntries(basedir).Select(o => dir + Path.GetFileName(o)).Where(o => o.StartsWith(cmd, StringComparison.OrdinalIgnoreCase) && !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase)) && !pushedResults.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase))).ToList();
                        autocomplete.Sort();
                        results.AddRange(autocomplete.ConvertAll(m => new Result()
                        {
                            Title = m,
                            SubTitle = "",
                            IcoPath = m,
                            Action = (c) =>
                            {
                                ExecuteCmd(m);
                                return true;
                            },
                            ContextMenu = GetContextMenus(m)
                        }));
                    }
                }
                catch (Exception) { }
            }
            return results;
        }

        private List<Result> GetContextMenus(string cmd)
        {
            return new List<Result>()
                     {
                        new Result()
                        {
                            Title = "Run As Administrator",
                            Action = c =>
                            {
                                context.API.HideApp();
                                ExecuteCmd(cmd, true);
                                return true;
                            },
                            IcoPath = "Images/cmd.png"
                        }
                     };
        }

        private void ExecuteCmd(string cmd, bool runAsAdministrator = false)
        {
            if (context.API.ShellRun(cmd, runAsAdministrator))
                CMDStorage.Instance.AddCmdHistory(cmd);
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
        }

        public override string ID
        {
            get { return "D409510CD0D2481F853690A07E6DC426"; }
        }

        public override string Name
        {
            get { return "Shell"; }
        }

        public override string IcoPath
        {
            get { return @"Images\cmd.png"; }
        }

        public override string Description
        {
            get { return "Provide executing commands from Wox. Commands should start with >"; }
        }

        public Control CreateSettingPanel()
        {
            return new CMDSetting();
        }
    }
}
