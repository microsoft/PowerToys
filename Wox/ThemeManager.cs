using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wox.Helper;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox
{
    internal class ThemeManager
    {
        private static List<string> themeDirectories = new List<string>();

        static ThemeManager()
        {
            themeDirectories.Add(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Themes"));

            string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
            if (userProfilePath != null)
            {
                themeDirectories.Add(Path.Combine(Path.Combine(userProfilePath, ".Wox"), "Themes"));
            }

            MakesureThemeDirectoriesExist();
        }

        private static void MakesureThemeDirectoriesExist()
        {
            foreach (string pluginDirectory in themeDirectories)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    Directory.CreateDirectory(pluginDirectory);
                }
            }
        }

        public static void ChangeTheme(string themeName)
        {
            string themePath = GetThemePath(themeName);
            if (string.IsNullOrEmpty(themePath))
            {
                themePath = GetThemePath("Dark");
                if (string.IsNullOrEmpty(themePath))
                {
                    throw new Exception("Change theme failed");
                }
            }

            var dict = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Absolute)
            };

            Style queryBoxStyle = dict["QueryBoxStyle"] as Style;
            if (queryBoxStyle != null)
            {
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily(UserSettingStorage.Instance.QueryBoxFont)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontStyle)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontWeight)));
                queryBoxStyle.Setters.Add(new Setter(TextBox.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(UserSettingStorage.Instance.QueryBoxFontStretch)));
            }

            Style resultItemStyle = dict["ItemTitleStyle"] as Style;
            Style resultSubItemStyle = dict["ItemSubTitleStyle"] as Style;
            Style resultItemSelectedStyle = dict["ItemTitleSelectedStyle"] as Style;
            Style resultSubItemSelectedStyle = dict["ItemSubTitleSelectedStyle"] as Style;
            if (resultItemStyle != null && resultSubItemStyle != null && resultSubItemSelectedStyle != null && resultItemSelectedStyle != null)
            {
                Setter fontFamily = new Setter(TextBlock.FontFamilyProperty, new FontFamily(UserSettingStorage.Instance.ResultItemFont));
                Setter fontStyle = new Setter(TextBlock.FontStyleProperty, FontHelper.GetFontStyleFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontStyle));
                Setter fontWeight = new Setter(TextBlock.FontWeightProperty, FontHelper.GetFontWeightFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontWeight));
                Setter fontStretch = new Setter(TextBlock.FontStretchProperty, FontHelper.GetFontStretchFromInvariantStringOrNormal(UserSettingStorage.Instance.ResultItemFontStretch));

                Setter[] setters = new Setter[] { fontFamily, fontStyle, fontWeight, fontStretch };
                Array.ForEach(new Style[] { resultItemStyle, resultSubItemStyle, resultItemSelectedStyle, resultSubItemSelectedStyle }, o => Array.ForEach(setters, p => o.Setters.Add(p)));
            }

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);

        }

        private static string GetThemePath(string themeName)
        {
            foreach (string themeDirectory in themeDirectories)
            {
                string path = Path.Combine(themeDirectory, themeName + ".xaml");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
