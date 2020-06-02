using ControlzEx.Theming;
using MahApps.Metro.Theming;
using System;
using System.Diagnostics;
using System.Windows;

namespace Wox.Core.Resource
{
    public class ThemeManager
    {
        private Theme currentTheme;
        private readonly Application App;
        private readonly string LightTheme = "Light.Accent1";
        private readonly string DarkTheme = "Dark.Accent1";
        private readonly string HighContrastTheme = "HighContrast.Accent1";

        public ThemeManager(Application app)
        {
            this.App = app;

            Uri LightThemeUri = new Uri("pack://application:,,,/Themes/Light.xaml");
            Uri DarkThemeUri = new Uri("pack://application:,,,/Themes/Dark.xaml");
            Uri HighContrastThemeUri = new Uri("pack://application:,,,/Themes/HighContrast.xaml");

            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(LightThemeUri,
                                            MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(DarkThemeUri,
                                                         MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(new LibraryTheme(HighContrastThemeUri,
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
                ChangeTheme(Theme.HighContrast);
            }
            else
            {
                string baseColor = WindowsThemeHelper.GetWindowsBaseColor();
                ChangeTheme((Theme)Enum.Parse(typeof(Theme), baseColor));
            }
        }

        private void ChangeTheme(Theme theme)
        {
            if (theme == currentTheme)
                return;
            if (theme == Theme.HighContrast)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.HighContrastTheme);
                currentTheme = Theme.HighContrast;
            }
            else if (theme == Theme.Light)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.LightTheme);
                currentTheme = Theme.Light;
            }
            else if (theme == Theme.Dark)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.DarkTheme);
                currentTheme = Theme.Dark;
            }
        }

        private void Current_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            Debug.WriteLine("Theme Updated: " + e.NewTheme);
            ResetTheme();
        }
    }

    enum Theme
    {
        Light, 
        Dark, 
        HighContrast
    }
}
