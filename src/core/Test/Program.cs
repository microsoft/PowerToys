using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using System;
using System.IO;
using System.Text.Json;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            PowerPreviewSettings pvst = new PowerPreviewSettings();
            pvst.name = "File Explorer";

            SndModuleSettings<PowerPreviewSettings> snd = new SndModuleSettings<PowerPreviewSettings>(pvst);

            Console.WriteLine(snd.ToString());
        }
    }
}
