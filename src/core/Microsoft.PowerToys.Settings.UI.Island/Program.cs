using System;
using System.Collections.Generic;
using System.Text;

namespace SettingsRunner
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            using (new SettingsUI.App())
            {
                SettingsRunner.App app = new SettingsRunner.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
