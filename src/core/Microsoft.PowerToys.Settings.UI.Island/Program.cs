using System;
using System.Collections.Generic;
using System.Text;

namespace SettingsMain
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            using (new SettingsUI.App())
            {
                SettingsMain.App app = new SettingsMain.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
