using Microsoft.Win32;
using PowerLauncher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Wox.Core.Resource
{
    public class ThemeManager
    {
        private string currentTheme
        {
            get => WindowsThemeHelper.getAppTheme();
        }

        private readonly Application App;
        
        public ThemeManager(Application app)
        {
            this.App = app;
            ResetTheme();

            SystemEvents.UserPreferenceChanged += (sender, args) =>
            {
                if (args.Category == UserPreferenceCategory.General)
                {
                    ResetTheme();
                }
            };
            SystemParameters.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemParameters.HighContrast))
                {
                    ResetTheme();
                }
            };
        }
        

        public void ResetTheme()
        {
            var dict = App.Resources.MergedDictionaries.First(c => ResourceDictionaryExtensions.GetName(c) == "Theme");
            dict.Source = new Uri($"/Themes/{currentTheme}.xaml", UriKind.Relative);
            App.Resources["ListViewItemBackground"] = new SolidColorBrush(WindowsThemeHelper.GetWindowsHighLightColor());
        }

        private Color ConvertColor(global::Windows.UI.Color color)
        {
            //Convert the specified UWP color to a WPF color
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }

}
