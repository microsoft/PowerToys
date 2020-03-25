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
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            OutGoingGeneralSettings outSettings = new OutGoingGeneralSettings(settings);
            Console.WriteLine(outSettings.ToString());
        }
    }
}
