using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using IniParser;
namespace Wox.Plugin.System
{
    public class PortableAppsProgramSource : AbstractProgramSource
    {
        public string BaseDirectory;

        public PortableAppsProgramSource(string baseDirectory)
        {
            BaseDirectory = baseDirectory;
        }

        public PortableAppsProgramSource(Wox.Infrastructure.UserSettings.ProgramSource source)
            : this(source.Location)
        {
            this.BonusPoints = source.BounsPoints;
        }

        public override List<Program> LoadPrograms()
        {
            List<Program> list = new List<Program>();
            var ini = new IniParser.Parser.IniDataParser();
            ini.Configuration.AllowDuplicateKeys = true;

            string menuSettingsPath = Path.Combine(BaseDirectory, @"PortableApps.com\Data\PortableAppsMenu.ini");

            IniParser.Model.KeyDataCollection appsRenamed = null, appsRecategorized = null, appsHidden = null;
            if (File.Exists(menuSettingsPath))
            {
                var menuSettings = ini.Parse(File.ReadAllText(menuSettingsPath, Encoding.Default));
                appsRenamed = menuSettings["AppsRenamed"];
                appsRecategorized = menuSettings["AppsRecategorized"];
                appsHidden = menuSettings["AppsHidden"];
            }
            if (appsRenamed == null) appsRenamed = new IniParser.Model.KeyDataCollection();
            if (appsRecategorized == null) appsRecategorized = new IniParser.Model.KeyDataCollection();
            if (appsHidden == null) appsHidden = new IniParser.Model.KeyDataCollection();

            foreach (var appDir in Directory.GetDirectories(BaseDirectory))
            {
                var appDirName = Path.GetDirectoryName(appDir);
                var appInfoPath = Path.Combine(appDir, @"App\AppInfo\appinfo.ini");
                var appInfoValid = false;

                if (File.Exists(appInfoPath))
                {
                    var appInfo = ini.Parse(File.ReadAllText(appInfoPath, Encoding.Default));
                    var appName = appInfo["Details"]["Name"] ?? appDirName;
                    var control = appInfo["Control"];
                    int count;
                    if (Int32.TryParse(control["Icons"], out count))
                    {
                        appInfoValid = true;
                        for (int i = 1; i <= count; i++)
                        {
                            string cmdline, name, icon;
                            cmdline = control[String.Format("Start{0}", i)];
                            name = control[String.Format("Name{0}", i)];
                            icon = control[String.Format("ExtractIcon{0}", i)];

                            if (i == 1)
                            {
                                if (cmdline == null) cmdline = control["Start"];
                                if (cmdline == null) continue;

                                if (name == null) name = appName;
                                if (icon == null) icon = control["ExtractIcon"];
                                if (icon == null && !File.Exists(icon = Path.Combine(appDir, @"App\AppInfo\appicon.ico"))) icon = null;
                            }

                            if (cmdline == null) continue;
                            if (name == null) name = String.Format("{0} #{1}", appName, i);
                            if (icon == null) icon = Path.Combine(appDir, String.Format(@"App\AppInfo\appicon{0}.ico", i));

                            cmdline = Path.Combine(appDir, cmdline);
                            var menuKey = (appDirName + @"\" + cmdline).ToLower();

                            var renamed = appsRenamed[menuKey];
                            if (renamed != null)
                                name = renamed;

                            var hidden = appsHidden[menuKey] == "true";

                            if (!hidden)
                            {
                                Program p = new Program()
                                {
                                    Title = name,
                                    IcoPath = icon,
                                    ExecutePath = cmdline
                                };
                                list.Add(p);
                            }
                        }
                    }
                }

                if (!appInfoValid)
                {
                    foreach (var item in Directory.GetFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        var menuKey = Path.GetFullPath(item).Substring(Path.GetFullPath(BaseDirectory).Length + 1).ToLower();

                        if (appsHidden[menuKey] != "true")
                        {
                            var p = CreateEntry(item);
                            var renamed = appsRenamed[menuKey];
                            if (renamed != null)
                                p.Title = renamed;

                            list.Add(p);
                        }
                    }
                }
            }

            return list;
        }
    }
}
