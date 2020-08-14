// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Plugin.Program.Logger;
using Microsoft.Win32;
using Wox.Infrastructure;
using Wox.Infrastructure.FileSystemHelper;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public class Win32Program : IProgram
    {
        public string Name { get; set; }

        public string UniqueIdentifier { get; set; }

        public string IcoPath { get; set; }

        public string FullPath { get; set; }

        public string LnkResolvedPath { get; set; }

        public string ParentDirectory { get; set; }

        public string ExecutableName { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool Valid { get; set; }

        public bool Enabled { get; set; }

        public bool HasArguments { get; set; } = false;

        public string Arguments { get; set; } = string.Empty;

        public string Location => ParentDirectory;

        public uint AppType { get; set; }

        // Wrappers for File Operations
        public static IFileVersionInfoWrapper FileVersionInfoWrapper { get; set; } = new FileVersionInfoWrapper();

        public static IFileWrapper FileWrapper { get; set; } = new FileWrapper();

        public static IShellLinkHelper Helper { get; set; } = new ShellLinkHelper();

        private const string ShortcutExtension = "lnk";
        private const string ApplicationReferenceExtension = "appref-ms";
        private const string ExeExtension = "exe";
        private const string InternetShortcutExtension = "url";

        private const string ProxyWebApp = "_proxy.exe";
        private const string AppIdArgument = "--app-id";

        private enum ApplicationTypes
        {
            WEB_APPLICATION = 0,
            INTERNET_SHORTCUT_APPLICATION = 1,
            WIN32_APPLICATION = 2,
            RUN_COMMAND = 3,
        }

        // Function to calculate the score of a result
        private int Score(string query)
        {
            var nameMatch = StringMatcher.FuzzySearch(query, Name);
            var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
            var executableNameMatch = StringMatcher.FuzzySearch(query, ExecutableName);
            var score = new[] { nameMatch.Score, descriptionMatch.Score / 2, executableNameMatch.Score }.Max();
            return score;
        }

        public bool IsWebApplication()
        {
            // To Filter PWAs when the user searches for the main application
            // All Chromium based applications contain the --app-id argument
            // Reference : https://codereview.chromium.org/399045/show
            bool isWebApplication = FullPath.Contains(ProxyWebApp, StringComparison.OrdinalIgnoreCase) && Arguments.Contains(AppIdArgument, StringComparison.OrdinalIgnoreCase);
            return isWebApplication;
        }

        // Condition to Filter pinned Web Applications or PWAs when searching for the main application
        public bool FilterWebApplication(string query)
        {
            // If the app is not a web application, then do not filter it
            if (!IsWebApplication())
            {
                return false;
            }

            // Set the subtitle to 'Web Application'
            AppType = (uint)ApplicationTypes.WEB_APPLICATION;

            string[] subqueries = query?.Split() ?? Array.Empty<string>();
            bool nameContainsQuery = false;
            bool pathContainsQuery = false;

            // check if any space separated query is a part of the app name or path name
            foreach (var subquery in subqueries)
            {
                if (FullPath.Contains(subquery, StringComparison.OrdinalIgnoreCase))
                {
                    pathContainsQuery = true;
                }

                if (Name.Contains(subquery, StringComparison.OrdinalIgnoreCase))
                {
                    nameContainsQuery = true;
                }
            }

            return pathContainsQuery && !nameContainsQuery;
        }

        // Function to set the subtitle based on the Type of application
        private string SetSubtitle(IPublicAPI api)
        {
            if (AppType == (uint)ApplicationTypes.WIN32_APPLICATION)
            {
                return api.GetTranslation("powertoys_run_plugin_program_win32_application");
            }
            else if (AppType == (uint)ApplicationTypes.INTERNET_SHORTCUT_APPLICATION)
            {
                return api.GetTranslation("powertoys_run_plugin_program_internet_shortcut_application");
            }
            else if (AppType == (uint)ApplicationTypes.WEB_APPLICATION)
            {
                return api.GetTranslation("powertoys_run_plugin_program_web_application");
            }
            else if (AppType == (uint)ApplicationTypes.RUN_COMMAND)
            {
                return api.GetTranslation("powertoys_run_plugin_program_run_command");
            }
            else
            {
                return string.Empty;
            }
        }

        public bool QueryEqualsNameForRunCommands(string query)
        {
            if (query != null && AppType == (uint)ApplicationTypes.RUN_COMMAND
                && !query.Equals(Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public Result Result(string query, IPublicAPI api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            var score = Score(query);
            if (score <= 0)
            { // no need to create result if this is zero
                return null;
            }

            if (!HasArguments)
            {
                var noArgumentScoreModifier = 5;
                score += noArgumentScoreModifier;
            }
            else
            {
                // Filter Web Applications when searching for the main application
                if (FilterWebApplication(query))
                {
                    return null;
                }
            }

            // NOTE: This is to display run commands only when there is an exact match, like in start menu
            if (!QueryEqualsNameForRunCommands(query))
            {
                return null;
            }

            var result = new Result
            {
                SubTitle = SetSubtitle(api),
                IcoPath = IcoPath,
                Score = score,
                ContextData = this,
                Action = e =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = LnkResolvedPath ?? FullPath,
                        WorkingDirectory = ParentDirectory,
                        UseShellExecute = true,
                    };

                    Main.StartProcess(Process.Start, info);

                    return true;
                },
            };

            // To set the title for the result to always be the name of the application
            result.Title = Name;
            result.TitleHighlightData = StringMatcher.FuzzySearch(query, Name).MatchData;

            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", api.GetTranslation("powertoys_run_plugin_program_file_name"), result.Title);
            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", api.GetTranslation("powertoys_run_plugin_program_file_path"), FullPath);
            result.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentially keeping the process alive.")]
        public List<ContextMenuResult> ContextMenus(IPublicAPI api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            var contextMenus = new List<ContextMenuResult>();

            if (AppType != (uint)ApplicationTypes.INTERNET_SHORTCUT_APPLICATION)
            {
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_run_as_administrator"),
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas",
                            UseShellExecute = true,
                        };

                        Task.Run(() => Main.StartProcess(Process.Start, info));

                        return true;
                    },
                });
            }

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Glyph = "\xE838",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.E,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        Main.StartProcess(Process.Start, new ProcessStartInfo("explorer", ParentDirectory));
                        return true;
                    },
                });

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = api.GetTranslation("wox_plugin_program_open_in_console"),
                    Glyph = "\xE756",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = (context) =>
                    {
                        try
                        {
                            Wox.Infrastructure.Helper.OpenInConsole(ParentDirectory);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"|Microsoft.Plugin.Program.Win32.ContextMenu| Failed to open {Name} in console, {e.Message}", e);
                            return false;
                        }
                    },
                });

            return contextMenus;
        }

        public override string ToString()
        {
            return ExecutableName;
        }

        private static Win32Program CreateWin32Program(string path)
        {
            try
            {
                var p = new Win32Program
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = path,
                    FullPath = path.ToLower(CultureInfo.CurrentCulture),
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true,
                    AppType = (uint)ApplicationTypes.WIN32_APPLICATION,
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException(
                    $"|Win32|Win32Program|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        // This function filters Internet Shortcut programs
        private static Win32Program InternetShortcutProgram(string path)
        {
            string[] lines = FileWrapper.ReadAllLines(path);
            string iconPath = string.Empty;
            string urlPath = string.Empty;
            bool validApp = false;

            Regex internetShortcutURLPrefixes = new Regex(@"^steam:\/\/(rungameid|run)\/|^com\.epicgames\.launcher:\/\/apps\/");

            const string urlPrefix = "URL=";
            const string iconFilePrefix = "IconFile=";

            foreach (string line in lines)
            {
                if (line.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    urlPath = line.Substring(urlPrefix.Length);
                    Uri uri = new Uri(urlPath);

                    // To filter out only those steam shortcuts which have 'run' or 'rungameid' as the hostname
                    if (internetShortcutURLPrefixes.Match(urlPath).Success)
                    {
                        validApp = true;
                    }
                }

                if (line.StartsWith(iconFilePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    iconPath = line.Substring(iconFilePrefix.Length);
                }
            }

            if (!validApp)
            {
                return new Win32Program() { Valid = false, Enabled = false };
            }

            try
            {
                var p = new Win32Program
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = iconPath,
                    FullPath = urlPath,
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Valid = true,
                    Enabled = true,
                    AppType = (uint)ApplicationTypes.INTERNET_SHORTCUT_APPLICATION,
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException(
                    $"|Win32|InternetShortcutProgram|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unsure of what exceptions are caught here while enabling static analysis")]
        private static Win32Program LnkProgram(string path)
        {
            var program = CreateWin32Program(path);
            try
            {
                const int MAX_PATH = 260;
                StringBuilder buffer = new StringBuilder(MAX_PATH);

                string target = Helper.RetrieveTargetPath(path);

                if (!string.IsNullOrEmpty(target))
                {
                    var extension = Extension(target);
                    if (extension == ExeExtension && File.Exists(target))
                    {
                        program.LnkResolvedPath = program.FullPath;
                        program.FullPath = Path.GetFullPath(target).ToLower(CultureInfo.CurrentCulture);
                        program.ExecutableName = Path.GetFileName(target);
                        program.HasArguments = Helper.HasArguments;
                        program.Arguments = Helper.Arguments;

                        var description = Helper.Description;
                        if (!string.IsNullOrEmpty(description))
                        {
                            program.Description = description;
                        }
                        else
                        {
                            var info = FileVersionInfoWrapper.GetVersionInfo(target);
                            if (!string.IsNullOrEmpty(info?.FileDescription))
                            {
                                program.Description = info.FileDescription;
                            }
                        }
                    }
                }

                return program;
            }

            // Only do a catch all in production. This is so make developer aware of any unhandled exception and add the exception handling in.
            // Error caused likely due to trying to get the description of the program
            catch (Exception e)
            {
                ProgramLogger.LogException(
                    $"|Win32|LnkProgram|{path}" +
                                                "|An unexpected error occurred in the calling method LnkProgram", e);

                program.Valid = false;
                return program;
            }
        }

        private static Win32Program ExeProgram(string path)
        {
            try
            {
                var program = CreateWin32Program(path);
                var info = FileVersionInfoWrapper.GetVersionInfo(path);

                if (!string.IsNullOrEmpty(info?.FileDescription))
                {
                    program.Description = info.FileDescription;
                }

                return program;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException(
                    $"|Win32|ExeProgram|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        // Function to get the Win32 application, given the path to the application
        public static Win32Program GetAppFromPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Win32Program app = null;
            const string exeExtension = ".exe";
            const string lnkExtension = ".lnk";
            const string urlExtenion = ".url";
            const string apprefExtension = ".appref-ms";

            string extension = Path.GetExtension(path);

            if (extension.Equals(exeExtension, StringComparison.OrdinalIgnoreCase))
            {
                app = ExeProgram(path);
            }
            else if (extension.Equals(lnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                app = LnkProgram(path);
            }
            else if (extension.Equals(apprefExtension, StringComparison.OrdinalIgnoreCase))
            {
                app = CreateWin32Program(path);
            }
            else if (extension.Equals(urlExtenion, StringComparison.OrdinalIgnoreCase))
            {
                app = InternetShortcutProgram(path);
            }

            // if the app is valid, only then return the application, else return null
            if (app?.Valid ?? false)
            {
                return app;
            }
            else
            {
                return null;
            }
        }

        private static IEnumerable<string> ProgramPaths(string directory, IList<string> suffixes, bool recursiveSearch = true)
        {
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
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
                            ProgramLogger.LogException(
                                $"|Win32|ProgramPaths|{currentDirectory}" +
                                                "|The directory trying to load the program from does not exist", e);
                        }
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.LogException(
                        $"|Win32|ProgramPaths|{currentDirectory}" +
                                                $"|Permission denied when trying to load programs from {currentDirectory}", e);
                }

                try
                {
                    // If the search is set to be non-recursive, then do not enqueue the child directories.
                    if (!recursiveSearch)
                    {
                        continue;
                    }

                    foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        folderQueue.Enqueue(childDirectory);
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.LogException(
                        $"|Win32|ProgramPaths|{currentDirectory}" +
                                                $"|Permission denied when trying to load programs from {currentDirectory}", e);
                }
            }
            while (folderQueue.Any());

            return files;
        }

        private static string Extension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLower(CultureInfo.CurrentCulture);

            if (!string.IsNullOrEmpty(extension))
            {
                return extension.Substring(1);
            }
            else
            {
                return string.Empty;
            }
        }

        private static ParallelQuery<Win32Program> UnregisteredPrograms(List<ProgramSource> sources, IList<string> suffixes)
        {
            var listToAdd = new List<string>();
            sources.Where(s => Directory.Exists(s.Location) && s.Enabled)
                .SelectMany(s => ProgramPaths(s.Location, suffixes))
                .ToList()
                .Where(t1 => !Main.Settings.DisabledProgramSources.Any(x => t1 == x.UniqueIdentifier))
                .ToList()
                .ForEach(x => listToAdd.Add(x));

            var paths = listToAdd.Distinct().ToArray();

            var programs1 = paths.AsParallel().Where(p => Extension(p) == ExeExtension).Select(ExeProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(ExeProgram);
            var programs3 = from p in paths.AsParallel()
                            let e = Extension(p)
                            where e != ShortcutExtension && e != ExeExtension
                            select CreateWin32Program(p);
            return programs1.Concat(programs2).Concat(programs3);
        }

        // Function to obtain the list of applications, the locations of which have been added to the env variable PATH
        private static ParallelQuery<Win32Program> PathEnvironmentPrograms(IList<string> suffixes)
        {
            // To get all the locations stored in the PATH env variable
            var pathEnvVariable = Environment.GetEnvironmentVariable("PATH");
            string[] searchPaths = pathEnvVariable.Split(Path.PathSeparator);
            IEnumerable<string> toFilterAllPaths = new List<string>();
            bool isRecursiveSearch = true;

            foreach (string path in searchPaths)
            {
                if (path.Length > 0)
                {
                    // to expand any environment variables present in the path
                    string directory = Environment.ExpandEnvironmentVariables(path);
                    var paths = ProgramPaths(directory, suffixes, !isRecursiveSearch);
                    toFilterAllPaths = toFilterAllPaths.Concat(paths);
                }
            }

            var allPaths = toFilterAllPaths
                        .Distinct()
                        .ToArray();

            var programs1 = allPaths.AsParallel().Where(p => Extension(p).Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(LnkProgram);
            var programs2 = allPaths.AsParallel().Where(p => Extension(p).Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase)).Select(CreateWin32Program);
            var programs3 = allPaths.AsParallel().Where(p => Extension(p).Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(InternetShortcutProgram);
            var programs4 = allPaths.AsParallel().Where(p => Extension(p).Equals(ExeExtension, StringComparison.OrdinalIgnoreCase)).Select(ExeProgram);

            var allPrograms = programs1.Concat(programs2).Where(p => p.Valid)
                .Concat(programs3).Where(p => p.Valid)
                .Concat(programs4).Where(p => p.Valid)
                .Select(p =>
                {
                    p.AppType = (uint)ApplicationTypes.RUN_COMMAND;
                    return p;
                });

            return allPrograms;
        }

        private static ParallelQuery<Win32Program> IndexPath(IList<string> suffixes, List<string> indexLocation)
        {
            var disabledProgramsList = Main.Settings.DisabledProgramSources;

            IEnumerable<string> toFilter = new List<string>();
            foreach (string location in indexLocation)
            {
                var programPaths = ProgramPaths(location, suffixes);
                toFilter = toFilter.Concat(programPaths);
            }

            var paths = toFilter
                        .Where(t1 => !disabledProgramsList.Any(x => x.UniqueIdentifier == t1))
                        .Select(t1 => t1)
                        .Distinct()
                        .ToArray();

            var programs1 = paths.AsParallel().Where(p => Extension(p).Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(LnkProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p).Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase)).Select(CreateWin32Program);
            var programs3 = paths.AsParallel().Where(p => Extension(p).Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(InternetShortcutProgram);
            var programs4 = paths.AsParallel().Where(p => Extension(p).Equals(ExeExtension, StringComparison.OrdinalIgnoreCase)).Select(ExeProgram);

            return programs1.Concat(programs2).Where(p => p.Valid)
                .Concat(programs3).Where(p => p.Valid)
                .Concat(programs4).Where(p => p.Valid);
        }

        private static ParallelQuery<Win32Program> StartMenuPrograms(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            List<string> indexLocation = new List<string>() { directory1, directory2 };

            return IndexPath(suffixes, indexLocation);
        }

        private static ParallelQuery<Win32Program> DesktopPrograms(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            List<string> indexLocation = new List<string>() { directory1 };

            return IndexPath(suffixes, indexLocation);
        }

        private static ParallelQuery<Win32Program> AppPathsPrograms(IList<string> suffixes)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            var programs = new List<Win32Program>();
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

            var disabledProgramsList = Main.Settings.DisabledProgramSources;
            var toFilter = programs.AsParallel().Where(p => suffixes.Contains(Extension(p.ExecutableName)));

            var filtered = toFilter.Where(t1 => !disabledProgramsList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier)).Select(t1 => t1);

            return filtered;
        }

        private static IEnumerable<Win32Program> GetProgramsFromRegistry(RegistryKey root)
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
                    {
                        return string.Empty;
                    }

                    var defaultValue = string.Empty;
                    path = key.GetValue(defaultValue) as string;
                }

                if (string.IsNullOrEmpty(path))
                {
                    return string.Empty;
                }

                // fix path like this: ""\"C:\\folder\\executable.exe\""
                return path = path.Trim('"', ' ');
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException(
                    $"|Win32|GetProgramPathFromRegistrySubKeys|{path}" +
                                            $"|Permission denied when trying to load the program from {path}", e);

                return string.Empty;
            }
        }

        private static Win32Program GetProgramFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new Win32Program();
            }

            path = Environment.ExpandEnvironmentVariables(path);

            if (!File.Exists(path))
            {
                return new Win32Program();
            }

            var entry = CreateWin32Program(path);
            entry.ExecutableName = Path.GetFileName(path);

            return entry;
        }

        // Overriding the object.GetHashCode() function to aid in removing duplicates while adding and removing apps from the concurrent dictionary storage
        public override int GetHashCode()
        {
            return new RemoveDuplicatesComparer().GetHashCode(this);
        }

        public override bool Equals(object obj)
        {
            return obj is Win32Program win && new RemoveDuplicatesComparer().Equals(this, win);
        }

        private class RemoveDuplicatesComparer : IEqualityComparer<Win32Program>
        {
            public bool Equals(Win32Program app1, Win32Program app2)
            {
                if (!string.IsNullOrEmpty(app1.Name) && !string.IsNullOrEmpty(app2.Name)
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
            public int GetHashCode(Win32Program obj)
            {
                int namePrime = 13;
                int executablePrime = 17;
                int fullPathPrime = 31;

                int result = 1;
                result = (result * namePrime) + obj.Name.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
                result = (result * executablePrime) + obj.ExecutableName.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
                result = (result * fullPathPrime) + obj.FullPath.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);

                return result;
            }
        }

        // Deduplication code
        public static Win32Program[] DeduplicatePrograms(ParallelQuery<Win32Program> programs)
        {
            var uniqueExePrograms = programs.Where(x => !(string.IsNullOrEmpty(x.LnkResolvedPath) && (Extension(x.FullPath) == ExeExtension) && !(x.AppType == (uint)ApplicationTypes.RUN_COMMAND)));
            var uniquePrograms = uniqueExePrograms.Distinct(new RemoveDuplicatesComparer());
            return uniquePrograms.ToArray();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Keeping the process alive but logging the exception")]
        public static Win32Program[] All(ProgramPluginSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            try
            {
                var programs = new List<Win32Program>().AsParallel();

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

                if (settings.EnablePathEnvironmentVariableSource)
                {
                    var appPathEnvironment = PathEnvironmentPrograms(settings.ProgramSuffixes);
                    programs = programs.Concat(appPathEnvironment);
                }

                if (settings.EnableDesktopSource)
                {
                    var desktop = DesktopPrograms(settings.ProgramSuffixes);
                    programs = programs.Concat(desktop);
                }

                return DeduplicatePrograms(programs);
            }
            catch (Exception e)
            {
                ProgramLogger.LogException("|Win32|All|Not available|An unexpected error occurred", e);

                return Array.Empty<Win32Program>();
            }
        }
    }
}
