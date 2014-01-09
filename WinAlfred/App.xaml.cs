using System;
using System.Threading;
using System.Windows;

namespace WinAlfred
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool startupFlag;
            Mutex mutex = new Mutex(true, "WinAlfred", out startupFlag);
            if (!startupFlag)
            {
                Environment.Exit(0);
            }
            else
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }
    }
}
