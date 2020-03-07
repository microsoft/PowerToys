using System;
using System.Collections.Generic;
using System.Text;

namespace settings_v2
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            using (new settingsui.App())
            {
                settings_v2.App app = new settings_v2.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
