using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Shell;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.Programs
{
    [Serializable]
    public class Win32
    {
        public string FullName { get; set; }
        public string IcoPath { get; set; }
        public string FullPath { get; set; }
        public string ParentDirectory { get; set; }
        public string ExecutableName { get; set; }
        public int Score { get; set; }

        private const string ShortcutExtension = "lnk";
        private const string ApplicationReferenceExtension = "appref-ms";
        private const string ExeExtension = "exe";

        public override string ToString()
        {
            return ExecutableName;
        }

        private static Win32 Win32Program(string path)
        {
            var p = new Win32
            {
                FullName = Path.GetFileNameWithoutExtension(path),
                IcoPath = path,
                FullPath = path,
                ParentDirectory = Directory.GetParent(path).FullName,
            };
            return p;
        }

        private static Win32 LnkProgram(string path)
        {
            var program = Win32Program(path);

            try
            {
                var link = new ShellLink();
                const uint STGM_READ = 0;
                ((IPersistFile)link).Load(path, STGM_READ);
                var hwnd = new _RemotableHandle();
                link.Resolve(ref hwnd, 0);

                const int MAX_PATH = 260;
                StringBuilder buffer = new StringBuilder(MAX_PATH);
                link.GetDescription(buffer, MAX_PATH);
                var description = buffer.ToString();
                if (!string.IsNullOrEmpty(description))
                {
                    program.FullName += $": {description}";
                }
                else
                {
                    buffer = new StringBuilder(MAX_PATH);
                    var data = new _WIN32_FIND_DATAW();
                    const uint SLGP_SHORTPATH = 1;
                    link.GetPath(buffer, buffer.Capacity, ref data, SLGP_SHORTPATH);
                    var target = buffer.ToString();
                    if (!string.IsNullOrEmpty(target))
                    {
                        var info = FileVersionInfo.GetVersionInfo(target);
                        if (!string.IsNullOrEmpty(info.FileDescription))
                        {
                            program.FullName += $": {info.FileDescription}";
                        }
                    }
                }
                return program;
            }
            catch (Exception)
            {
                Log.Error($"Error when parsing shortcut: {path}");
                return program;
            }
        }

        private static Win32 ExeProgram(string path)
        {
            var program = Win32Program(path);
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrEmpty(info.FileDescription))
            {
                program.FullName += $": {info.FileDescription}";
            }
            return program;
        }

        private static IEnumerable<string> ProgramPaths(string directory, string[] suffixes)
        {
            if (Directory.Exists(directory))
            {
                var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(
                    f => suffixes.Contains(Extension(f))
                );
                return files;
            }
            else
            {
                return new string[] { };
            }
        }

        private static string Extension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLower();
            if (!string.IsNullOrEmpty(extension))
            {
                return extension.Substring(1);
            }
            else
            {
                return string.Empty;
            }
        }

        private static ParallelQuery<Win32> UnregisteredPrograms(List<Settings.ProgramSource> sources, string[] suffixes)
        {
            var paths = sources.Where(s => Directory.Exists(s.Location))
                               .SelectMany(s => ProgramPaths(s.Location, suffixes))
                               .ToArray();
            var programs1 = paths.AsParallel().Where(p => Extension(p) == ExeExtension).Select(ExeProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(ExeProgram);
            var programs3 = from p in paths.AsParallel()
                            let e = Extension(p)
                            where e != ShortcutExtension && e != ExeExtension
                            select Win32Program(p);
            return programs1.Concat(programs2).Concat(programs3);
        }

        private static ParallelQuery<Win32> StartMenuPrograms(string[] suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            var paths1 = ProgramPaths(directory1, suffixes);
            var paths2 = ProgramPaths(directory2, suffixes);
            var paths = paths1.Concat(paths2).ToArray();
            var programs1 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(LnkProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p) == ApplicationReferenceExtension).Select(Win32Program);
            return programs1.Concat(programs2);
        }


        private static ParallelQuery<Win32> AppPathsPrograms(string[] suffixes)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            var programs = new List<Win32>();
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
            var filtered = programs.AsParallel().Where(p => suffixes.Contains(Extension(p.ExecutableName)));
            return filtered;
        }

        private static IEnumerable<Win32> ProgramsFromRegistryKey(RegistryKey root)
        {
            var programs = root.GetSubKeyNames()
                               .Select(subkey => ProgramFromRegistrySubkey(root, subkey))
                               .Where(p => !string.IsNullOrEmpty(p.FullName));
            return programs;
        }

        private static Win32 ProgramFromRegistrySubkey(RegistryKey root, string subkey)
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
                            var entry = Win32Program(path);
                            entry.ExecutableName = subkey;
                            return entry;
                        }
                    }
                }
            }
            return new Win32();
        }

        private static Win32 ScoreFilter(Win32 p)
        {
            var start = new[] { "启动", "start" };
            var doc = new[] { "帮助", "help", "文档", "documentation" };
            var uninstall = new[] { "卸载", "uninstall" };

            var contained = start.Any(s => p.FullName.ToLower().Contains(s));
            if (contained)
            {
                p.Score += 10;
            }
            contained = doc.Any(d => p.FullName.ToLower().Contains(d));
            if (contained)
            {
                p.Score -= 10;
            }
            contained = uninstall.Any(u => p.FullName.ToLower().Contains(u));
            if (contained)
            {
                p.Score -= 20;
            }

            return p;
        }

        public static Win32[] All(Settings settings)
        {
            ParallelQuery<Win32> programs = new List<Win32>().AsParallel();
            if (settings.EnableRegistrySource)
            {
                var appPaths = AppPathsPrograms(settings.ProgramSuffixes);
                programs = programs.Concat(appPaths);
            }
            if (settings.EnableStartMenuSource)
            {
                var startMenu = StartMenuPrograms(settings.ProgramSuffixes);
                programs = programs.Concat(startMenu);
            }
            var unregistered = UnregisteredPrograms(settings.ProgramSources, settings.ProgramSuffixes);
            programs = programs.Concat(unregistered).Select(ScoreFilter);
            return programs.ToArray();
        }
    }
}
