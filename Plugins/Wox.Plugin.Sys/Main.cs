using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Wox.Infrastructure;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using FormsApplication = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;

namespace Wox.Plugin.Sys
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n
    {
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

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hWnd, uint dwFlags);

        // http://www.pinvoke.net/default.aspx/Enums/HRESULT.html
        private enum HRESULT : uint
        {
            S_FALSE = 0x0001,
            S_OK = 0x0000
        }

        #endregion

        public Control CreateSettingPanel()
        {
            var results = Commands();
            return new SysSettings(results);
        }

        public List<Result> Query(Query query)
        {
            var commands = Commands();
            var results = new List<Result>();
            foreach (var c in commands)
            {
                var titleScore = StringMatcher.Score(c.Title, query.Search);
                var subTitleScore = StringMatcher.Score(c.SubTitle, query.Search);
                var score = Math.Max(titleScore, subTitleScore);
                if (score > 0)
                {
                    c.Score = score;
                    results.Add(c);
                }
            }
            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        private List<Result> Commands()
        {
            var results = new List<Result>();
            results.AddRange(new[]
            {
                new Result
                {
                    Title = "Shutdown",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_shutdown_computer"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        var reuslt = MessageBox.Show("Are you sure you want to shut the computer down?",
                                                     "Shutdown Computer?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (reuslt == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/s /t 0");
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_restart_computer"),
                    IcoPath = "Images\\restart.png",
                    Action = c =>
                    {
                        var result = MessageBox.Show("Are you sure you want to restart the computer?",
                                                     "Restart Computer?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/r /t 0");
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Log off",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_log_off"),
                    IcoPath = "Images\\logoff.png",
                    Action = c => ExitWindowsEx(EWX_LOGOFF, 0)
                },
                new Result
                {
                    Title = "Lock",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_lock"),
                    IcoPath = "Images\\lock.png",
                    Action = c =>
                    {
                        LockWorkStation();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Sleep",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_sleep"),
                    IcoPath = "Images\\sleep.png",
                    Action = c => FormsApplication.SetSuspendState(PowerState.Suspend, false, false)
                },
                new Result
                {
                    Title = "Empty Recycle Bin",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_emptyrecyclebin"),
                    IcoPath = "Images\\recyclebin.png",
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = SHEmptyRecycleBin(new WindowInteropHelper(Application.Current.MainWindow).Handle, 0);
                        if (result != (uint) HRESULT.S_OK && result != (uint)0x8000FFFF)
                        {
                            MessageBox.Show($"Error emptying recycle bin, error code: {result}\n" +
                                            "please refer to https://msdn.microsoft.com/en-us/library/windows/desktop/aa378137",
                                            "Error",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Exit",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_exit"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart Wox",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_restart"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.RestarApp();
                        return false;
                    }
                },
                new Result
                {
                    Title = "Settings",
                    SubTitle = context.API.GetTranslation("wox_plugin_sys_setting"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.OpenSettingDialog();
                        return true;
                    }
                }
            });
            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_sys_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_sys_plugin_description");
        }
    }
}