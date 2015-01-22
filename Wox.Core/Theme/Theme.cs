using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wox.Core.UI;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;

namespace Wox.Core.Theme
{
    public class Theme : IUIResource,ITheme
    {
        private static List<string> themeDirectories = new List<string>();

        static Theme()
        {
            themeDirectories.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Themes"));
            MakesureThemeDirectoriesExist();
        }

        private static void MakesureThemeDirectoriesExist()
        {
            foreach (string pluginDirectory in themeDirectories)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(pluginDirectory);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e.Message);
                    }
                }
            }
        }

        public void ChangeTheme(string themeName)
        {
            string themePath = GetThemePath(themeName);
            if (string.IsNullOrEmpty(themePath))
            {
                themePath = GetThemePath("Dark");
                if (string.IsNullOrEmpty(themePath))
                {
                    throw new System.Exception("Change theme failed");
                }
            }

            UserSettingStorage.Instance.Theme = themeName;
            UserSettingStorage.Instance.Save();

            ResourceMerger.ApplyResources();
        }

        public ResourceDictionary GetResourceDictionary()
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(GetThemePath(UserSettingStorage.Instance.Theme), UriKind.Absolute)
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

            return dict;
        }

        public List<string> LoadAvailableThemes()
        {
            List<string> themes = new List<string>();
            foreach (var themeDirectory in themeDirectories)
            {
                themes.AddRange(
                    Directory.GetFiles(themeDirectory)
                        .Where(filePath => filePath.EndsWith(".xaml") && !filePath.EndsWith("Base.xaml"))
                        .ToList());
            }
            return themes.OrderBy(o => o).ToList();
        }

        private string GetThemePath(string themeName)
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
