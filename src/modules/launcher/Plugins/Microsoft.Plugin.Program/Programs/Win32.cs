using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Wox.Infrastructure;
using Microsoft.Plugin.Program.Logger;
using Wox.Plugin;
using System.Windows.Input;
using System.Reflection;

namespace Microsoft.Plugin.Program.Programs
{

    [Serializable]
    public class Win32 : IProgram
    {
        public string Name { get; set; }
        public string UniqueIdentifier { get; set; }
        public string IcoPath { get; set; }
        public string FullPath { get; set; }
        public string LnkResolvedPath { get; set; }
        public string ParentDirectory { get; set; }
        public string ExecutableName { get; set; }
        public string Description { get; set; }
        public bool Valid { get; set; }
        public bool Enabled { get; set; }
        public bool hasArguments { get; set; } = false;
        public string Location => ParentDirectory;

        private const string ShortcutExtension = "lnk";
        private const string ApplicationReferenceExtension = "appref-ms";
        private const string ExeExtension = "exe";

        private int Score(string query)
        {
            var nameMatch = StringMatcher.FuzzySearch(query, Name);
            var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
            var executableNameMatch = StringMatcher.FuzzySearch(query, ExecutableName);
            var score = new[] { nameMatch.Score, descriptionMatch.Score, executableNameMatch.Score }.Max();
            return score;
        }


        public Result Result(string query, IPublicAPI api)
        {
            var score = Score(query);
            if (score <= 0)
            { // no need to create result if this is zero
                return null;
            }

            if(!hasArguments)
            {
                var noArgumentScoreModifier = 5;
                score += noArgumentScoreModifier;
            }

            var result = new Result
            {
                SubTitle = "Win32 application",
                IcoPath = IcoPath,
                Score = score,
                ContextData = this,
                Action = e =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = LnkResolvedPath ?? FullPath,
                        WorkingDirectory = ParentDirectory,
                        UseShellExecute = true
                    };

                    Main.StartProcess(Process.Start, info);

                    return true;
                }
            };

            if (Description.Length >= Name.Length &&
                Description.Substring(0, Name.Length) == Name)
            {
                result.Title = Description;
                result.TitleHighlightData = StringMatcher.FuzzySearch(query, Description).MatchData;
            }
            else
            {
                result.Title = Name;
                result.TitleHighlightData = StringMatcher.FuzzySearch(query, Name).MatchData;
            }

            return result;
        }


        public List<ContextMenuResult> ContextMenus(IPublicAPI api)
        {
            var contextMenus = new List<ContextMenuResult>
            {
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas",
                            UseShellExecute = true
                        };

                        Task.Run(() => Main.StartProcess(Process.Start, info));

                        return true;
                    }
                },
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Glyph = "\xE838",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.E,
                    AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                    Action = _ =>
                    {


                        Main.StartProcess(Process.Start, new ProcessStartInfo("explorer", ParentDirectory));

                        return true;
                    }
                }
            };
            return contextMenus;
        }



        public override string ToString()
        {
            return ExecutableName;
        }

        private static Win32 Win32Program(string path)
        {
            try
            {
                var p = new Win32
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = path,
                    FullPath = path.ToLower(),
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|Win32Program|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return new Win32() { Valid = false, Enabled = false };
            }
        }

        private static Win32 LnkProgram(string path)
        {
            var program = Win32Program(path);
            try
            {
                const int MAX_PATH = 260;
                StringBuilder buffer = new StringBuilder(MAX_PATH);
                ShellLinkHelper _helper = new ShellLinkHelper();
                string target = _helper.retrieveTargetPath(path);

                if (!string.IsNullOrEmpty(target))
                {
                    var extension = Extension(target);
                    if (extension == ExeExtension && File.Exists(target))
                    {
                        program.LnkResolvedPath = program.FullPath;
                        program.FullPath = Path.GetFullPath(target).ToLower();
                        program.ExecutableName = Path.GetFileName(target);
                        program.hasArguments = _helper.hasArguments;

                        var description = _helper.description;
                        if (!string.IsNullOrEmpty(description))
                        {
                            program.Description = description;
                        }
                        else
                        {
                            var info = FileVersionInfo.GetVersionInfo(target);
                            if (!string.IsNullOrEmpty(info.FileDescription))
                            {
                                program.Description = info.FileDescription;
                            }
                        }
                    }
                }
                return program;
            }
            catch (COMException e)
            {
                // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always cause exception
                ProgramLogger.LogException($"|Win32|LnkProgram|{path}" +
                                                "|Error caused likely due to trying to get the description of the program", e);

                program.Valid = false;
                return program;
            }
#if !DEBUG //Only do a catch all in production. This is so make developer aware of any unhandled exception and add the exception handling in.
            catch (Exception e)
            {
                ProgramLogger.LogException($"|Win32|LnkProgram|{path}" +
                                                "|An unexpected error occurred in the calling method LnkProgram", e);

                program.Valid = false;
                return program;
            }
#endif
        }

        private static Win32 ExeProgram(string path)
        {
            try
            {
                var program = Win32Program(path);
                var info = FileVersionInfo.GetVersionInfo(path);

                if (!string.IsNullOrEmpty(info.FileDescription))
                {
                    program.Description = info.FileDescription;
                }

                return program;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|ExeProgram|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return new Win32() { Valid = false, Enabled = false };
            }
        }

        private static IEnumerable<string> ProgramPaths(string directory, string[] suffixes)
        {
            if (!Directory.Exists(directory))
            {
                return new string[] { };
            }

            var files = new List<string>();
            var folderQueue = new Queue<string>();
            folderQueue.Enqueue(directory);
            
            do
            {
                var currentDirectory = folderQueue.Dequeue();
                try
                {
                    foreach (var suffix in suffixes)
                    {
                        try
                        {
                            files.AddRange(Directory.EnumerateFiles(currentDirectory, $"*.{suffix}", SearchOption.TopDirectoryOnly));
                        }
                        catch (DirectoryNotFoundException e)
                        {
                            ProgramLogger.LogException($"|Win32|ProgramPaths|{currentDirectory}" +
                                                "|The directory trying to load the program from does not exist", e);
                        }
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.LogException($"|Win32|ProgramPaths|{currentDirectory}" +
                                                $"|Permission denied when trying to load programs from {currentDirectory}", e);
                }

                try
                {
                    foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        folderQueue.Enqueue(childDirectory);
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.LogException($"|Win32|ProgramPaths|{currentDirectory}" +
                                                $"|Permission denied when trying to load programs from {currentDirectory}", e);
                }
            } while (folderQueue.Any());

            return files;
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
            var listToAdd = new List<string>();
            sources.Where(s => Directory.Exists(s.Location) && s.Enabled)
                .SelectMany(s => ProgramPaths(s.Location, suffixes))
                .ToList()
                .Where(t1 => !Main._settings.DisabledProgramSources.Any(x => t1 == x.UniqueIdentifier))
                .ToList()
                .ForEach(x => listToAdd.Add(x));

            var paths = listToAdd.Distinct().ToArray();

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
            var disabledProgramsList = Main._settings.DisabledProgramSources;

            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            var paths1 = ProgramPaths(directory1, suffixes);
            var paths2 = ProgramPaths(directory2, suffixes);

            var toFilter = paths1.Concat(paths2);
            var paths = toFilter
                        .Where(t1 => !disabledProgramsList.Any(x => x.UniqueIdentifier == t1))
                        .Select(t1 => t1)
                        .Distinct()
                        .ToArray();

            var programs1 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(LnkProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p) == ApplicationReferenceExtension).Select(Win32Program);

            return programs1.Concat(programs2).Where(p => p.Valid);
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
                    programs.AddRange(GetProgramsFromRegistry(root));
                }
            }
            using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
            {
                if (root != null)
                {
                    programs.AddRange(GetProgramsFromRegistry(root));
                }
            }

            var disabledProgramsList = Main._settings.DisabledProgramSources;
            var toFilter = programs.AsParallel().Where(p => suffixes.Contains(Extension(p.ExecutableName)));

            var filtered = toFilter.Where(t1 => !disabledProgramsList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier)).Select(t1 => t1);

            return filtered;
        }

        private static IEnumerable<Win32> GetProgramsFromRegistry(RegistryKey root)
        {
            return root
                    .GetSubKeyNames()
                    .Select(x => GetProgramPathFromRegistrySubKeys(root, x))
                    .Distinct()
                    .Select(x => GetProgramFromPath(x));
        }

        private static string GetProgramPathFromRegistrySubKeys(RegistryKey root, string subkey)
        {
            var path = string.Empty;
            try
            {
                using (var key = root.OpenSubKey(subkey))
                {
                    if (key == null)
                        return string.Empty;

                    var defaultValue = string.Empty;
                    path = key.GetValue(defaultValue) as string;
                }

                if (string.IsNullOrEmpty(path))
                    return string.Empty;

                // fix path like this: ""\"C:\\folder\\executable.exe\""
                return path = path.Trim('"', ' ');
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|GetProgramPathFromRegistrySubKeys|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return string.Empty;
            }
        }

        private static Win32 GetProgramFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new Win32();

            path = Environment.ExpandEnvironmentVariables(path);

            if (!File.Exists(path))
                return new Win32();

            var entry = Win32Program(path);
            entry.ExecutableName = Path.GetFileName(path);

            return entry;
        }

        public class removeDuplicatesComparer : IEqualityComparer<Win32>
        {
            public bool Equals(Win32 app1, Win32 app2)
            {
                
                if(!string.IsNullOrEmpty(app1.Name) && !string.IsNullOrEmpty(app2.Name)
                    && !string.IsNullOrEmpty(app1.ExecutableName) && !string.IsNullOrEmpty(app2.ExecutableName)
                    && !string.IsNullOrEmpty(app1.FullPath) && !string.IsNullOrEmpty(app2.FullPath))
                {
                    return app1.Name.Equals(app2.Name, StringComparison.OrdinalIgnoreCase) 
                        && app1.ExecutableName.Equals(app2.ExecutableName, StringComparison.OrdinalIgnoreCase) 
                        && app1.FullPath.Equals(app2.FullPath, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }

            // Ref : https://stackoverflow.com/questions/2730865/how-do-i-calculate-a-good-hash-code-for-a-list-of-strings
            public int GetHashCode(Win32 obj)
            {
                int namePrime = 13;
                int executablePrime = 17;
                int fullPathPrime = 31;

                int result = 1;
                result = result * namePrime + obj.Name.GetHashCode();
                result = result * executablePrime + obj.ExecutableName.GetHashCode();
                result = result * fullPathPrime + obj.FullPath.GetHashCode();

                return result;
            }
        }

        // Deduplication code
        public static Func<ParallelQuery<Win32>, Win32[]> DeduplicatePrograms = (programs) =>
        {
            var uniqueExePrograms = programs.Where(x => !string.IsNullOrEmpty(x.LnkResolvedPath) || Extension(x.FullPath) != ExeExtension);
            var uniquePrograms = uniqueExePrograms.Distinct(new removeDuplicatesComparer());
            return uniquePrograms.ToArray();
        };

        public static Win32[] All(Settings settings)
        {
            try
            {
                var programs = new List<Win32>().AsParallel();

                var unregistered = UnregisteredPrograms(settings.ProgramSources, settings.ProgramSuffixes);
                programs = programs.Concat(unregistered);

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

                return DeduplicatePrograms(programs);
            }
#if DEBUG //This is to make developer aware of any unhandled exception and add in handling.
            catch (Exception e)
            {
                throw e;
            }
#endif

#if !DEBUG //Only do a catch all in production.
            catch (Exception e)
            {
                ProgramLogger.LogException("|Win32|All|Not available|An unexpected error occurred", e);

                return new Win32[0];
            }
#endif
        }
    }
}
