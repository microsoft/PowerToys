using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public class AppPathsPrograms : Win32
    {
        public override List<Program> LoadPrograms()
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            var programs = new List<Program>();
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            using (var root = Registry.LocalMachine.OpenSubKey(appPaths))
            {
                if (root != null)
                {
                    programs.AddRange(ProgramsFromRegistryKey(root));
                }
            }
            using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
            {
                if (root != null)
                {
                    programs.AddRange(ProgramsFromRegistryKey(root));
                }
            }
            return programs;
        }

        private IEnumerable<Program> ProgramsFromRegistryKey(RegistryKey root)
        {
            var programs = root.GetSubKeyNames()
                               .Select(subkey => ProgramFromRegistrySubkey(root, subkey))
                               .Where(p => !string.IsNullOrEmpty(p.Title));
            return programs;
        }

        private Program ProgramFromRegistrySubkey(RegistryKey root, string subkey)
        {
            using (var key = root.OpenSubKey(subkey))
            {
                if (key != null)
                {
                    var defaultValue = string.Empty;
                    var path = key.GetValue(defaultValue) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        // fix path like this: ""\"C:\\folder\\executable.exe\""
                        path = path.Trim('"');
                        path = Environment.ExpandEnvironmentVariables(path);

                        if (File.Exists(path))
                        {
                            var entry = CreateEntry(path);
                            entry.ExecutableName = subkey;
                            return entry;
                        }
                    }
                }
            }
            return new Program();
        }
    }
}