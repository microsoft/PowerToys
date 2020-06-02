using ControlzEx.Theming;
using MahApps.Metro.Theming;
using System;

namespace Wox.Core.Resource
{
    public class Theme
    {
        readonly Uri LightTheme = new Uri("pack://application:,,,/Light.xaml");
        readonly Uri DarkTheme = new Uri("pack://application:,,,/Dark.xaml");
        readonly Uri HighContrastTheme = new Uri("pack://application:,,,/HighContrast.xaml");

        Theme()
        {
            ThemeManager.Current.AddLibraryTheme(new LibraryTheme(LightTheme,
                                            MahAppsLibraryThemeProvider.DefaultInstance));
            ThemeManager.Current.AddLibraryTheme(new LibraryTheme(DarkTheme,
                                                         MahAppsLibraryThemeProvider.DefaultInstance));
            ThemeManager.Current.AddLibraryTheme(new LibraryTheme(HighContrastTheme,
                                                        MahAppsLibraryThemeProvider.DefaultInstance));
        }
    }
}
