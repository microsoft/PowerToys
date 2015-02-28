using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin.Features;
using Control = System.Windows.Controls.Control;

namespace Wox.Plugin.CMD
{
    public class CMD : IPlugin, ISettingProvider, IPluginI18n, IInstantQuery, IExclusiveQuery, IContextMenu
    {
        private PluginInitContext context;
        private bool WinRStroked;
        private readonly KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            List<Result> pushedResults = new List<Result>();
            if (query.Search == ">")
            {
                return GetAllHistoryCmds();
            }

            if (query.Search.StartsWith(">") && query.Search.Length > 1)
            {
                string cmd = query.Search.Substring(1);
                var queryCmd = GetCurrentCmd(cmd);
                context.API.PushResults(query, context.CurrentPluginMetadata, new List<Result>() { queryCmd });
                pushedResults.Add(queryCmd);

                var history = GetHistoryCmds(cmd, queryCmd);
                context.API.PushResults(query, context.CurrentPluginMetadata, history);
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
                            IcoPath = "Images/cmd.png",
                            Action = (c) =>
                            {
                                ExecuteCmd(m);
                                return true;
                            }
                        }));
                    }
                }
                catch (Exception) { }

            }
            return results;
        }

        private List<Result> GetHistoryCmds(string cmd, Result result)
        {
            IEnumerable<Result> history = CMDStorage.Instance.CMDHistory.Where(o => o.Key.Contains(cmd))
                .OrderByDescending(o => o.Value)
                .Select(m =>
                {
                    if (m.Key == cmd)
                    {
                        result.SubTitle = string.Format(context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value);
                        return null;
                    }

                    var ret = new Result
                    {
                        Title = m.Key,
                        SubTitle =  string.Format(context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value),
                        IcoPath = "Images/cmd.png",
                        Action = (c) =>
                        {
                            ExecuteCmd(m.Key);
                            return true;
                        }
                    };
                    return ret;
                }).Where(o => o != null).Take(4);
            return history.ToList();
        }

        private Result GetCurrentCmd(string cmd)
        {
            Result result = new Result
            {
                Title = cmd,
                Score = 5000,
                SubTitle = context.API.GetTranslation("wox_plugin_cmd_execute_through_shell"),
                IcoPath = "Images/cmd.png",
                Action = (c) =>
                {
                    ExecuteCmd(cmd);
                    return true;
                }
            };

            return result;
        }

        private List<Result> GetAllHistoryCmds()
        {
            IEnumerable<Result> history = CMDStorage.Instance.CMDHistory.OrderByDescending(o => o.Value)
                .Select(m => new Result
                {
                    Title = m.Key,
                    SubTitle =  string.Format(context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value),
                    IcoPath = "Images/cmd.png",
                    Action = (c) =>
                    {
                        ExecuteCmd(m.Key);
                        return true;
                    }
                }).Take(5);
            return history.ToList();
        }

        private void ExecuteCmd(string cmd, bool runAsAdministrator = false)
        {
            if (context.API.ShellRun(cmd, runAsAdministrator))
                CMDStorage.Instance.AddCmdHistory(cmd);
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            context.API.GlobalKeyboardEvent += API_GlobalKeyboardEvent;
        }

        bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state)
        {
            if (CMDStorage.Instance.ReplaceWinR)
            {
                if (keyevent == (int)KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.R && state.WinPressed)
                {
                    WinRStroked = true;
                    OnWinRPressed();
                    return false;
                }
                if (keyevent == (int)KeyEvent.WM_KEYUP && WinRStroked && vkcode == (int)Keys.LWin)
                {
                    WinRStroked = false;
                    keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            return true;
        }

        private void OnWinRPressed()
        {
            context.API.ShowApp();
            context.API.ChangeQuery(">");
        }

        public Control CreateSettingPanel()
        {
            return new CMDSetting();
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_cmd_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_cmd_plugin_description");
        }

        public bool IsInstantQuery(string query)
        {
            if (query.StartsWith(">")) return true;
            return false;
        }

        public bool IsExclusiveQuery(Query query)
        {
            return query.Search.StartsWith(">");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>()
                     {
                        new Result()
                        {
                            Title = context.API.GetTranslation("wox_plugin_cmd_run_as_administrator"),
                            Action = c =>
                            {
                                context.API.HideApp();
                                ExecuteCmd(selectedResult.Title, true);
                                return true;
                            },
                            IcoPath = "Images/cmd.png"
                        }
                     };
        }
    }
}