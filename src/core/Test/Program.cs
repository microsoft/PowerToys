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
            PowerPreviewSettings settings = new PowerPreviewSettings("Image Resizer");
            
            Console.WriteLine(settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value);
        }
    }
}
