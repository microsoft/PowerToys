using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    static class VSCodeInstances
    {
        private static readonly string PathUserAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private static string _systemPath = String.Empty;

        private static string _userAppDataPath = Environment.GetEnvironmentVariable("AppData");

        public static List<VSCodeInstance> instances = new List<VSCodeInstance>();

        // Gets the executablePath and AppData foreach instance of VSCode
        public static void LoadVSCodeInstances()
        {
            if (_systemPath != Environment.GetEnvironmentVariable("PATH"))
            {

                instances = new List<VSCodeInstance>();

                _systemPath = Environment.GetEnvironmentVariable("PATH");
                var paths = _systemPath.Split(";");
                paths = paths.Where(x => x.Contains("VS Code")).ToArray();
                foreach (var path in paths)
                {
                    var files = Directory.GetFiles(path);
                    files = files.Where(x => x.Contains("code") && !x.EndsWith(".cmd")).ToArray();

                    if (files.Length > 0)
                    {
                        var file = files[0];
                        if (file.EndsWith("code"))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = file,
                                AppData = Path.Combine(_userAppDataPath, "Code"),
                                VSCodeVersion = VSCodeVersion.Stable
                            });
                        }
                        else if (file.EndsWith("code-insiders"))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = file,
                                AppData = Path.Combine(_userAppDataPath, "Code - Insiders"),
                                VSCodeVersion = VSCodeVersion.Insiders
                            });
                        }
                        else if (file.EndsWith("code-exploration"))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = file,
                                AppData = Path.Combine(_userAppDataPath, "Code - Exploration"),
                                VSCodeVersion = VSCodeVersion.Exploration
                            });
                        }
                    }
                }
            }
        }
    }
}
