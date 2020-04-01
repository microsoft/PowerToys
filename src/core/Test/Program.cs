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
            PowerRenameSettings pvst = new PowerRenameSettings();
            pvst.name = "File Explorer";

            TestSome(pvst);


        }

        public static void TestSome(IPowerToySettings settings)
        {
            Console.WriteLine(settings.IPCOutMessage());
        }
    }
}
