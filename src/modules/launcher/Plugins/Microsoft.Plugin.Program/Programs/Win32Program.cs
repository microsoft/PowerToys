// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
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
using Wox.Plugin;
using Wox.Plugin.Logger;
using DirectoryWrapper = Wox.Infrastructure.FileSystemHelper.DirectoryWrapper;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public class Win32Program : IProgram
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

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

        public bool HasArguments { get; set; }

        public string Arguments { get; set; } = string.Empty;

        public string Location => ParentDirectory;

        public ApplicationType AppType { get; set; }

        // Wrappers for File Operations
        public static IFileVersionInfoWrapper FileVersionInfoWrapper { get; set; } = new FileVersionInfoWrapper();

        public static IFile FileWrapper { get; set; } = new FileSystem().File;

        public static IShellLinkHelper ShellLinkHelper { get; set; } = new ShellLinkHelper();

        public static IDirectoryWrapper DirectoryWrapper { get; set; } = new DirectoryWrapper();

        private const string ShortcutExtension = "lnk";
        private const string ApplicationReferenceExtension = "appref-ms";
        private const string InternetShortcutExtension = "url";
        private static readonly HashSet<string> ExecutableApplicationExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "exe", "bat", "bin", "com", "msc", "msi", "cmd", "ps1", "job", "msp", "mst", "sct", "ws", "wsh", "wsf" };

        private const string ProxyWebApp = "_proxy.exe";
        private const string AppIdArgument = "--app-id";

        public enum ApplicationType
        {
            WebApplication = 0,
            InternetShortcutApplication = 1,
            Win32Application = 2,
            ShortcutApplication = 3,
            ApprefApplication = 4,
            RunCommand = 5,
            Folder = 6,
            GenericFile = 7,
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
            // Using Ordinal IgnoreCase since this is used internally
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
            AppType = ApplicationType.WebApplication;

            string[] subqueries = query?.Split() ?? Array.Empty<string>();
            bool nameContainsQuery = false;
            bool pathContainsQuery = false;

            // check if any space separated query is a part of the app name or path name
            foreach (var subquery in subqueries)
            {
                // Using OrdinalIgnoreCase since these are used internally
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
        private string SetSubtitle()
        {
            if (AppType == ApplicationType.Win32Application || AppType == ApplicationType.ShortcutApplication || AppType == ApplicationType.ApprefApplication)
            {
                return Properties.Resources.powertoys_run_plugin_program_win32_application;
            }
            else if (AppType == ApplicationType.InternetShortcutApplication)
            {
                return Properties.Resources.powertoys_run_plugin_program_internet_shortcut_application;
            }
            else if (AppType == ApplicationType.WebApplication)
            {
                return Properties.Resources.powertoys_run_plugin_program_web_application;
            }
            else if (AppType == ApplicationType.RunCommand)
            {
                return Properties.Resources.powertoys_run_plugin_program_run_command;
            }
            else if (AppType == ApplicationType.Folder)
            {
                return Properties.Resources.powertoys_run_plugin_program_folder_type;
            }
            else if (AppType == ApplicationType.GenericFile)
            {
                return Properties.Resources.powertoys_run_plugin_program_generic_file_type;
            }
            else
            {
                return string.Empty;
            }
        }

        public bool QueryEqualsNameForRunCommands(string query)
        {
            if (query != null && AppType == ApplicationType.RunCommand)
            {
                // Using OrdinalIgnoreCase since this is used internally
                if (!query.Equals(Name, StringComparison.OrdinalIgnoreCase) && !query.Equals(ExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public Result Result(string query, string queryArguments, IPublicAPI api)
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
                SubTitle = SetSubtitle(),
                IcoPath = IcoPath,
                Score = score,
                ContextData = this,
                ProgramArguments = queryArguments,
                Action = e =>
                {
                    var info = GetProcessStartInfo(queryArguments);

                    Main.StartProcess(Process.Start, info);

                    return true;
                },
            };

            // To set the title for the result to always be the name of the application
            result.Title = Name;
            result.SetTitleHighlightData(StringMatcher.FuzzySearch(query, Name).MatchData);

            // Using CurrentCulture since this is user facing
            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", Properties.Resources.powertoys_run_plugin_program_file_name, result.Title);
            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", Properties.Resources.powertoys_run_plugin_program_file_path, FullPath);
            result.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally keeping the process alive.")]
        public List<ContextMenuResult> ContextMenus(string queryArguments, IPublicAPI api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            var contextMenus = new List<ContextMenuResult>();

            if (AppType != ApplicationType.InternetShortcutApplication && AppType != ApplicationType.Folder && AppType != ApplicationType.GenericFile)
            {
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_run_as_administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        var info = GetProcessStartInfo(queryArguments, true);
                        Task.Run(() => Main.StartProcess(Process.Start, info));

                        return true;
                    },
                });
            }

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_open_containing_folder,
                    Glyph = "\xE838",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.E,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        Helper.OpenInShell(ParentDirectory);
                        return true;
                    },
                });

            contextMenus.Add(
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_open_in_console,
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
                            Log.Exception($"|Failed to open {Name} in console, {e.Message}", e, GetType());
                            return false;
                        }
                    },
                });

            return contextMenus;
        }

        private ProcessStartInfo GetProcessStartInfo(string programArguments, bool runAsAdmin = false)
        {
            return new ProcessStartInfo
            {
                FileName = LnkResolvedPath ?? FullPath,
                WorkingDirectory = ParentDirectory,
                UseShellExecute = true,
                Arguments = programArguments,
                Verb = runAsAdmin ? "runas" : string.Empty,
            };
        }

        public override string ToString()
        {
            return ExecutableName;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any error in CreateWin32Program should not prevent other programs from loading.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "User facing path needs to be shown in lowercase.")]
        private static Win32Program CreateWin32Program(string path)
        {
            try
            {
                var p = new Win32Program
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = path,

                    // Using InvariantCulture since this is user facing
                    FullPath = path.ToLowerInvariant(),
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true,
                    AppType = ApplicationType.Win32Application,
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.Exception($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method CreateWin32Program at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        // This function filters Internet Shortcut programs
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any error in InternetShortcutProgram should not prevent other programs from loading.")]
        private static Win32Program InternetShortcutProgram(string path)
        {
            try
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
                    // Using OrdinalIgnoreCase since this is used internally
                    if (line.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        urlPath = line.Substring(urlPrefix.Length);

                        try
                        {
                            Uri uri = new Uri(urlPath);
                        }
                        catch (UriFormatException e)
                        {
                            // To catch the exception if the uri cannot be parsed.
                            // Link to watson crash: https://watsonportal.microsoft.com/Failure?FailureSearchText=5f871ea7-e886-911f-1b31-131f63f6655b
                            ProgramLogger.Exception($"url could not be parsed", e, MethodBase.GetCurrentMethod().DeclaringType, urlPath);
                            return new Win32Program() { Valid = false, Enabled = false };
                        }

                        // To filter out only those steam shortcuts which have 'run' or 'rungameid' as the hostname
                        if (internetShortcutURLPrefixes.Match(urlPath).Success)
                        {
                            validApp = true;
                        }
                    }

                    // Using OrdinalIgnoreCase since this is used internally
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
                        AppType = ApplicationType.InternetShortcutApplication,
                    };
                    return p;
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.Exception($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                    return new Win32Program() { Valid = false, Enabled = false };
                }
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method InternetShortcutProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Unsure of what exceptions are caught here while enabling static analysis")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "User facing path needs to be shown in lowercase.")]
        private static Win32Program LnkProgram(string path)
        {
            try
            {
                var program = CreateWin32Program(path);
                const int MAX_PATH = 260;
                StringBuilder buffer = new StringBuilder(MAX_PATH);

                string target = ShellLinkHelper.RetrieveTargetPath(path);

                if (!string.IsNullOrEmpty(target))
                {
                    if (File.Exists(target) || Directory.Exists(target))
                    {
                        program.LnkResolvedPath = program.FullPath;

                        // Using InvariantCulture since this is user facing
                        program.FullPath = Path.GetFullPath(target).ToLowerInvariant();
                        program.AppType = GetAppTypeFromPath(target);

                        var description = ShellLinkHelper.Description;
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
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method LnkProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any error in ExeProgram should not prevent other programs from loading.")]
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
                ProgramLogger.Exception($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
            catch (FileNotFoundException e)
            {
                ProgramLogger.Exception($"|Unable to locate exe file at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method ExeProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return new Win32Program() { Valid = false, Enabled = false };
            }
        }

        // Function to get the application type, given the path to the application
        public static ApplicationType GetAppTypeFromPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            string extension = Extension(path);
            ApplicationType appType = ApplicationType.GenericFile;

            // Using OrdinalIgnoreCase since these are used internally with paths
            if (ExecutableApplicationExtensions.Contains(extension))
            {
                appType = ApplicationType.Win32Application;
            }
            else if (extension.Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase))
            {
                appType = ApplicationType.ShortcutApplication;
            }
            else if (extension.Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase))
            {
                appType = ApplicationType.ApprefApplication;
            }
            else if (extension.Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase))
            {
                appType = ApplicationType.InternetShortcutApplication;
            }

            // If the path exists, check if it is a directory
            else if (DirectoryWrapper.Exists(path))
            {
                appType = ApplicationType.Folder;
            }

            return appType;
        }

        // Function to get the Win32 application, given the path to the application
        public static Win32Program GetAppFromPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Win32Program app = null;

            ApplicationType appType = GetAppTypeFromPath(path);

            if (appType == ApplicationType.Win32Application)
            {
                app = ExeProgram(path);
            }
            else if (appType == ApplicationType.ShortcutApplication)
            {
                app = LnkProgram(path);
            }
            else if (appType == ApplicationType.ApprefApplication)
            {
                app = CreateWin32Program(path);
                app.AppType = ApplicationType.ApprefApplication;
            }
            else if (appType == ApplicationType.InternetShortcutApplication)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Minimise the effect of error on other programs")]
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
                            ProgramLogger.Exception("|The directory trying to load the program from does not exist", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                        }
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.Exception($"|Permission denied when trying to load programs from {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                }
                catch (Exception e)
                {
                    ProgramLogger.Exception($"|An unexpected error occurred in the calling method ProgramPaths at {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
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
                    ProgramLogger.Exception($"|Permission denied when trying to load programs from {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                }
                catch (Exception e)
                {
                    ProgramLogger.Exception($"|An unexpected error occurred in the calling method ProgramPaths at {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                }
            }
            while (folderQueue.Any());

            return files;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "User facing path needs to be shown in lowercase.")]
        private static string Extension(string path)
        {
            // Using InvariantCulture since this is user facing
            var extension = Path.GetExtension(path)?.ToLowerInvariant();

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

            var programs1 = paths.AsParallel().Where(p => ExecutableApplicationExtensions.Contains(Extension(p))).Select(ExeProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p) == ShortcutExtension).Select(LnkProgram);
            var programs3 = from p in paths.AsParallel()
                            let e = Extension(p)
                            where e != ShortcutExtension && !ExecutableApplicationExtensions.Contains(e)
                            select CreateWin32Program(p);
            return programs1.Concat(programs2).Where(p => p.Valid).Concat(programs3).Where(p => p.Valid);
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

            // Using OrdinalIgnoreCase since this is used internally with paths
            var programs1 = allPaths.AsParallel().Where(p => Extension(p).Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(LnkProgram);
            var programs2 = allPaths.AsParallel().Where(p => Extension(p).Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase)).Select(CreateWin32Program);
            var programs3 = allPaths.AsParallel().Where(p => Extension(p).Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(InternetShortcutProgram);
            var programs4 = allPaths.AsParallel().Where(p => ExecutableApplicationExtensions.Contains(Extension(p))).Select(ExeProgram);

            var allPrograms = programs1.Concat(programs2).Where(p => p.Valid)
                .Concat(programs3).Where(p => p.Valid)
                .Concat(programs4).Where(p => p.Valid)
                .Select(p =>
                {
                    p.AppType = ApplicationType.RunCommand;
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

            // Using OrdinalIgnoreCase since this is used internally with paths
            var programs1 = paths.AsParallel().Where(p => Extension(p).Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(LnkProgram);
            var programs2 = paths.AsParallel().Where(p => Extension(p).Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase)).Select(CreateWin32Program);
            var programs3 = paths.AsParallel().Where(p => Extension(p).Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase)).Select(InternetShortcutProgram);
            var programs4 = paths.AsParallel().Where(p => ExecutableApplicationExtensions.Contains(Extension(p))).Select(ExeProgram);

            return programs1.Concat(programs2).Where(p => p.Valid)
                .Concat(programs3).Where(p => p.Valid)
                .Concat(programs4).Where(p => p.Valid);
        }

        private static ParallelQuery<Win32Program> StartMenuPrograms(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            List<string> indexLocation = new List<string>() { directory1, directory2 };

            return IndexPath(suffixes, indexLocation);
        }

        private static ParallelQuery<Win32Program> DesktopPrograms(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            List<string> indexLocation = new List<string>() { directory1, directory2 };

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
                ProgramLogger.Exception($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

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
            return RemoveDuplicatesComparer.Default.GetHashCode(this);
        }

        public override bool Equals(object obj)
        {
            return obj is Win32Program win && RemoveDuplicatesComparer.Default.Equals(this, win);
        }

        private class RemoveDuplicatesComparer : IEqualityComparer<Win32Program>
        {
            public static readonly RemoveDuplicatesComparer Default = new RemoveDuplicatesComparer();

            public bool Equals(Win32Program app1, Win32Program app2)
            {
                if (!string.IsNullOrEmpty(app1.Name) && !string.IsNullOrEmpty(app2.Name)
                    && !string.IsNullOrEmpty(app1.ExecutableName) && !string.IsNullOrEmpty(app2.ExecutableName)
                    && !string.IsNullOrEmpty(app1.FullPath) && !string.IsNullOrEmpty(app2.FullPath))
                {
                    // Using OrdinalIgnoreCase since this is used internally
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

                // Using Ordinal since this is used internally
                result = (result * namePrime) + obj.Name.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
                result = (result * executablePrime) + obj.ExecutableName.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);
                result = (result * fullPathPrime) + obj.FullPath.ToUpperInvariant().GetHashCode(StringComparison.Ordinal);

                return result;
            }
        }

        // Deduplication code
        public static Win32Program[] DeduplicatePrograms(ParallelQuery<Win32Program> programs)
        {
            var uniqueExePrograms = programs.Where(x => !(string.IsNullOrEmpty(x.LnkResolvedPath) && ExecutableApplicationExtensions.Contains(Extension(x.FullPath)) && x.AppType != ApplicationType.RunCommand));
            return new HashSet<Win32Program>(uniqueExePrograms, new RemoveDuplicatesComparer()).ToArray();
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
                ProgramLogger.Exception("An unexpected error occurred", e, MethodBase.GetCurrentMethod().DeclaringType, "Not available");

                return Array.Empty<Win32Program>();
            }
        }
    }
}
