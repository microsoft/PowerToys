using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Runner
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            using (new UI.App())
            {
                App app = new App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
