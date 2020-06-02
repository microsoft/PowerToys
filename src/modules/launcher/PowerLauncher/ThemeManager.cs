using ControlzEx.Theming;
using MahApps.Metro.Theming;
using System;
using System.Diagnostics;
using System.Windows;

namespace Wox.Core.Resource
{
    public class ThemeManager
    {
        private string currentTheme;
        private readonly Application App;
        private readonly Uri LightTheme = new Uri("pack://application:,,,/Themes/Light.xaml");
        private readonly Uri DarkTheme = new Uri("pack://application:,,,/Themes/Dark.xaml");
        private readonly Uri HighContrastTheme = new Uri("pack://application:,,,/Themes/HighContrast.xaml");

        public ThemeManager(Application app)
        {
            this.App = app;
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(LightTheme,
                                            MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(DarkTheme,
                                                         MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(HighContrastTheme,
                                                        MahAppsLibraryThemeProvider.DefaultInstance));

            ResetTheme();
            ControlzEx.Theming.ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ControlzEx.Theming.ThemeManager.Current.ThemeChanged += Current_ThemeChanged;
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
            if (SystemParameters.HighContrast)
            {
                ChangeTheme("HighContrast");
            }
            else
            {
                string baseColor = WindowsThemeHelper.GetWindowsBaseColor();
                ChangeTheme(baseColor);
            }
        }

        private void ChangeTheme(String theme)
        {
            if (theme == currentTheme)
                return;
            if (theme == "HighContrast")
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, "HighContrast.Accent1");
                currentTheme = "HighContrast";
            }
            else if (theme == "Light")
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, "Light.Accent1");
                currentTheme = "Light";
            }
            else if (theme == "Dark")
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, "Dark.Accent1");
                currentTheme = "Dark";
            }
        }

        private void Current_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            Debug.WriteLine("New Theme: " + e.NewTheme);
            ResetTheme();
        }
    }

}
