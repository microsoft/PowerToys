using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    static class VSCodeInstances
    {
        private static readonly string PathUserAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private static string _systemPath = String.Empty;

        private static string _userAppDataPath = Environment.GetEnvironmentVariable("AppData");

        public static List<VSCodeInstance> instances = new List<VSCodeInstance>();

        // Gets the executablePath and AppData for each instance of VSCode
        public static void LoadVSCodeInstances()
        {
            if (_systemPath != Environment.GetEnvironmentVariable("PATH"))
            {

                instances = new List<VSCodeInstance>();

                _systemPath = Environment.GetEnvironmentVariable("PATH");
                var paths = _systemPath.Split(";");
                foreach (var path in paths)
                {
                    if (path.Contains("\\Microsoft VS Code\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code.exe"));
                        if (File.Exists(path_executable))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
                                AppData = Path.Combine(_userAppDataPath, "Code"),
                                VSCodeVersion = VSCodeVersion.Stable
                            });
                        }
                    }
                    else if (path.Contains("\\Microsoft VS Code Insiders\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code - Insiders.exe"));
                        if (File.Exists(path_executable))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
                                AppData = Path.Combine(_userAppDataPath, "Code - Insiders"),
                                VSCodeVersion = VSCodeVersion.Insiders
                            });
                        }
                    }
                    else if (path.Contains("\\Microsoft VS Code Exploration\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code - Exploration.exe"));
                        if (File.Exists(path_executable))
                        {
                            instances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
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
