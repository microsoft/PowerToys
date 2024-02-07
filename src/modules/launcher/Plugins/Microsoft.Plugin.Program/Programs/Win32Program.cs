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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Plugin.Program.Logger;
using Microsoft.Plugin.Program.Utils;
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
        public static readonly Win32Program InvalidProgram = new Win32Program { Valid = false, Enabled = false };

        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        public string Name { get; set; }

        // Localized name based on windows display language
        public string NameLocalized { get; set; } = string.Empty;

        public string UniqueIdentifier { get; set; }

        public string IcoPath { get; set; }

        public string Description { get; set; } = string.Empty;

        // Path of app executable or lnk target executable
        public string FullPath { get; set; }

        // Localized path based on windows display language
        public string FullPathLocalized { get; set; } = string.Empty;

        public string ParentDirectory { get; set; }

        public string ExecutableName { get; set; }

        // Localized executable name based on windows display language
        public string ExecutableNameLocalized { get; set; } = string.Empty;

        // Path to the lnk file on LnkProgram
        public string LnkFilePath { get; set; }

        public string LnkResolvedExecutableName { get; set; }

        // Localized path based on windows display language
        public string LnkResolvedExecutableNameLocalized { get; set; } = string.Empty;

        public bool Valid { get; set; }

        public bool Enabled { get; set; }

        public bool HasArguments => !string.IsNullOrEmpty(Arguments);

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
        private static readonly HashSet<string> ExecutableApplicationExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "exe", "bat", "bin", "com", "cpl", "msc", "msi", "cmd", "ps1", "job", "msp", "mst", "sct", "ws", "wsh", "wsf" };

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
            var locNameMatch = StringMatcher.FuzzySearch(query, NameLocalized);
            var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
            var executableNameMatch = StringMatcher.FuzzySearch(query, ExecutableName);
            var locExecutableNameMatch = StringMatcher.FuzzySearch(query, ExecutableNameLocalized);
            var lnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableName);
            var locLnkResolvedExecutableNameMatch = StringMatcher.FuzzySearch(query, LnkResolvedExecutableNameLocalized);
            var score = new[] { nameMatch.Score, locNameMatch.Score, descriptionMatch.Score / 2, executableNameMatch.Score, locExecutableNameMatch.Score, lnkResolvedExecutableNameMatch.Score, locLnkResolvedExecutableNameMatch.Score }.Max();
            return score;
        }

        public bool IsWebApplication()
        {
            // To Filter PWAs when the user searches for the main application
            // All Chromium based applications contain the --app-id argument
            // Reference : https://codereview.chromium.org/399045
            // Using Ordinal IgnoreCase since this is used internally
            return !string.IsNullOrEmpty(FullPath) &&
                   !string.IsNullOrEmpty(Arguments) &&
                   FullPath.Contains(ProxyWebApp, StringComparison.OrdinalIgnoreCase) &&
                   Arguments.Contains(AppIdArgument, StringComparison.OrdinalIgnoreCase);
        }

        // Condition to Filter pinned Web Applications or PWAs when searching for the main application
        public bool FilterWebApplication(string query)
        {
            // If the app is not a web application, then do not filter it
            if (!IsWebApplication())
            {
                return false;
            }

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
        private string GetSubtitle()
        {
            switch (AppType)
            {
                case ApplicationType.Win32Application:
                case ApplicationType.ShortcutApplication:
                case ApplicationType.ApprefApplication:
                    return Properties.Resources.powertoys_run_plugin_program_win32_application;
                case ApplicationType.InternetShortcutApplication:
                    return Properties.Resources.powertoys_run_plugin_program_internet_shortcut_application;
                case ApplicationType.WebApplication:
                    return Properties.Resources.powertoys_run_plugin_program_web_application;
                case ApplicationType.RunCommand:
                    return Properties.Resources.powertoys_run_plugin_program_run_command;
                case ApplicationType.Folder:
                    return Properties.Resources.powertoys_run_plugin_program_folder_type;
                case ApplicationType.GenericFile:
                    return Properties.Resources.powertoys_run_plugin_program_generic_file_type;
                default:
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
            ArgumentNullException.ThrowIfNull(api);

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
                // To set the title for the result to always be the name of the application
                Title = !string.IsNullOrEmpty(NameLocalized) ? NameLocalized : Name,
                SubTitle = GetSubtitle(),
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

            // Adjust title of RunCommand result
            if (AppType == ApplicationType.RunCommand)
            {
                result.Title = ExecutableName;
            }

            result.TitleHighlightData = StringMatcher.FuzzySearch(query, result.Title).MatchData;

            // Using CurrentCulture since this is user facing
            var toolTipTitle = result.Title;
            string filePath = !string.IsNullOrEmpty(FullPathLocalized) ? FullPathLocalized : FullPath;
            var toolTipText = filePath;
            result.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);

            return result;
        }

        public List<ContextMenuResult> ContextMenus(string queryArguments, IPublicAPI api)
        {
            ArgumentNullException.ThrowIfNull(api);

            var contextMenus = new List<ContextMenuResult>();

            if (AppType != ApplicationType.InternetShortcutApplication && AppType != ApplicationType.Folder && AppType != ApplicationType.GenericFile)
            {
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_run_as_administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        var info = GetProcessStartInfo(queryArguments, RunAsType.Administrator);
                        Task.Run(() => Main.StartProcess(Process.Start, info));

                        return true;
                    },
                });

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_program_run_as_user,
                    Glyph = "\xE7EE",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.U,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        var info = GetProcessStartInfo(queryArguments, RunAsType.OtherUser);
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
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
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
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = (context) =>
                    {
                        try
                        {
                            Helper.OpenInConsole(ParentDirectory);
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

        private ProcessStartInfo GetProcessStartInfo(string programArguments, RunAsType runAs = RunAsType.None)
        {
            return new ProcessStartInfo
            {
                FileName = LnkFilePath ?? FullPath,
                WorkingDirectory = ParentDirectory,
                UseShellExecute = true,
                Arguments = programArguments,
                Verb = runAs == RunAsType.Administrator ? "runAs" : runAs == RunAsType.OtherUser ? "runAsUser" : string.Empty,
            };
        }

        private enum RunAsType
        {
            None,
            Administrator,
            OtherUser,
        }

        public override string ToString()
        {
            return ExecutableName;
        }

        private static Win32Program CreateWin32Program(string path)
        {
            try
            {
                return new Win32Program
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = path,

                    // Using InvariantCulture since this is user facing
                    FullPath = path,
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true,
                    AppType = ApplicationType.Win32Application,

                    // Localized name, path and executable based on windows display language
                    NameLocalized = Main.ShellLocalizationHelper.GetLocalizedName(path),
                    FullPathLocalized = Main.ShellLocalizationHelper.GetLocalizedPath(path),
                    ExecutableNameLocalized = Path.GetFileName(Main.ShellLocalizationHelper.GetLocalizedPath(path)),
                };
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.Warn($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method CreateWin32Program at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
        }

        private static readonly Regex InternetShortcutURLPrefixes = new Regex(@"^steam:\/\/(rungameid|run|open)\/|^com\.epicgames\.launcher:\/\/apps\/", RegexOptions.Compiled);

        // This function filters Internet Shortcut programs
        private static Win32Program InternetShortcutProgram(string path)
        {
            try
            {
                // We don't want to read the whole file if we don't need to
                var lines = FileWrapper.ReadLines(path);
                string iconPath = string.Empty;
                string urlPath = string.Empty;
                bool validApp = false;

                const string urlPrefix = "URL=";
                const string iconFilePrefix = "IconFile=";

                foreach (string line in lines)
                {
                    // Using OrdinalIgnoreCase since this is used internally
                    if (line.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        urlPath = line.Substring(urlPrefix.Length);

                        if (!Uri.TryCreate(urlPath, UriKind.RelativeOrAbsolute, out Uri _))
                        {
                            ProgramLogger.Warn("url could not be parsed", null, MethodBase.GetCurrentMethod().DeclaringType, urlPath);
                            return InvalidProgram;
                        }

                        // To filter out only those steam shortcuts which have 'run' or 'rungameid' as the hostname
                        if (InternetShortcutURLPrefixes.Match(urlPath).Success)
                        {
                            validApp = true;
                        }
                    }
                    else if (line.StartsWith(iconFilePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        iconPath = line.Substring(iconFilePrefix.Length);
                    }

                    // If we resolved an urlPath & and an iconPath quit reading the file
                    if (!string.IsNullOrEmpty(urlPath) && !string.IsNullOrEmpty(iconPath))
                    {
                        break;
                    }
                }

                if (!validApp)
                {
                    return InvalidProgram;
                }

                try
                {
                    return new Win32Program
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
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.Warn($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                    return InvalidProgram;
                }
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method InternetShortcutProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
        }

        private static Win32Program LnkProgram(string path)
        {
            try
            {
                var program = CreateWin32Program(path);
                string target = ShellLinkHelper.RetrieveTargetPath(path);

                if (!string.IsNullOrEmpty(target))
                {
                    if (!(File.Exists(target) || Directory.Exists(target)))
                    {
                        // If the link points nowhere, consider it invalid.
                        return InvalidProgram;
                    }

                    program.LnkFilePath = program.FullPath;
                    program.LnkResolvedExecutableName = Path.GetFileName(target);
                    program.LnkResolvedExecutableNameLocalized = Path.GetFileName(Main.ShellLocalizationHelper.GetLocalizedPath(target));

                    // Using CurrentCulture since this is user facing
                    program.FullPath = Path.GetFullPath(target);
                    program.FullPathLocalized = Main.ShellLocalizationHelper.GetLocalizedPath(target);

                    program.Arguments = ShellLinkHelper.Arguments;

                    // A .lnk could be a (Chrome) PWA, set correct AppType
                    program.AppType = program.IsWebApplication()
                        ? ApplicationType.WebApplication
                        : GetAppTypeFromPath(target);

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

                return program;
            }
            catch (System.IO.FileLoadException e)
            {
                ProgramLogger.Warn($"Couldn't load the link file at {path}. This might be caused by a new link being created and locked by the OS.", e, MethodBase.GetCurrentMethod().DeclaringType, path);
                return InvalidProgram;
            }

            // Only do a catch all in production. This is so make developer aware of any unhandled exception and add the exception handling in.
            // Error caused likely due to trying to get the description of the program
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method LnkProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
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
                ProgramLogger.Warn($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
            catch (FileNotFoundException e)
            {
                ProgramLogger.Warn($"|Unable to locate exe file at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
            catch (Exception e)
            {
                ProgramLogger.Exception($"|An unexpected error occurred in the calling method ExeProgram at {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return InvalidProgram;
            }
        }

        // Function to get the application type, given the path to the application
        public static ApplicationType GetAppTypeFromPath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            string extension = Extension(path);

            // Using OrdinalIgnoreCase since these are used internally with paths
            if (ExecutableApplicationExtensions.Contains(extension))
            {
                return ApplicationType.Win32Application;
            }
            else if (extension.Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase))
            {
                return ApplicationType.ShortcutApplication;
            }
            else if (extension.Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase))
            {
                return ApplicationType.ApprefApplication;
            }
            else if (extension.Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase))
            {
                return ApplicationType.InternetShortcutApplication;
            }
            else if (string.IsNullOrEmpty(extension) && DirectoryWrapper.Exists(path))
            {
                return ApplicationType.Folder;
            }

            return ApplicationType.GenericFile;
        }

        // Function to get the Win32 application, given the path to the application
        public static Win32Program GetAppFromPath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            Win32Program app;
            switch (GetAppTypeFromPath(path))
            {
                case ApplicationType.Win32Application:
                    app = ExeProgram(path);
                    break;
                case ApplicationType.ShortcutApplication:
                    app = LnkProgram(path);
                    break;
                case ApplicationType.ApprefApplication:
                    app = CreateWin32Program(path);
                    app.AppType = ApplicationType.ApprefApplication;
                    break;
                case ApplicationType.InternetShortcutApplication:
                    app = InternetShortcutProgram(path);
                    break;
                case ApplicationType.WebApplication:
                case ApplicationType.RunCommand:
                case ApplicationType.Folder:
                case ApplicationType.GenericFile:
                default:
                    app = null;
                    break;
            }

            // if the app is valid, only then return the application, else return null
            return app?.Valid == true
                ? app
                : null;
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

            // Keep track of already visited directories to avoid cycles.
            var alreadyVisited = new HashSet<string>();

            do
            {
                var currentDirectory = folderQueue.Dequeue();

                if (alreadyVisited.Contains(currentDirectory))
                {
                    continue;
                }

                alreadyVisited.Add(currentDirectory);

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
                            ProgramLogger.Warn("|The directory trying to load the program from does not exist", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                        }
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.Warn($"|Permission denied when trying to load programs from {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
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

                    foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory, "*", new EnumerationOptions()
                    {
                        // https://learn.microsoft.com/dotnet/api/system.io.enumerationoptions?view=net-6.0
                        // Exclude directories with the Reparse Point file attribute, to avoid loops due to symbolic links / directory junction / mount points.
                        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
                        RecurseSubdirectories = false,
                    }))
                    {
                        folderQueue.Enqueue(childDirectory);
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    ProgramLogger.Warn($"|Permission denied when trying to load programs from {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                }
                catch (Exception e)
                {
                    ProgramLogger.Exception($"|An unexpected error occurred in the calling method ProgramPaths at {currentDirectory}", e, MethodBase.GetCurrentMethod().DeclaringType, currentDirectory);
                }
            }
            while (folderQueue.Count > 0);

            return files;
        }

        private static string Extension(string path)
        {
            // Using InvariantCulture since this is user facing
            var extension = Path.GetExtension(path)?.ToLowerInvariant();

            return !string.IsNullOrEmpty(extension)
                ? extension.Substring(1)
                : string.Empty;
        }

        private static IEnumerable<string> CustomProgramPaths(IEnumerable<ProgramSource> sources, IList<string> suffixes)
            => sources?.Where(programSource => Directory.Exists(programSource.Location) && programSource.Enabled)
                .SelectMany(programSource => ProgramPaths(programSource.Location, suffixes))
                .ToList() ?? Enumerable.Empty<string>();

        // Function to obtain the list of applications, the locations of which have been added to the env variable PATH
        private static List<string> PathEnvironmentProgramPaths(IList<string> suffixes)
        {
            // To get all the locations stored in the PATH env variable
            var pathEnvVariable = Environment.GetEnvironmentVariable("PATH");
            string[] searchPaths = pathEnvVariable.Split(Path.PathSeparator);
            var toFilterAllPaths = new List<string>();
            bool isRecursiveSearch = true;

            foreach (string path in searchPaths)
            {
                if (path.Length > 0)
                {
                    // to expand any environment variables present in the path
                    string directory = Environment.ExpandEnvironmentVariables(path);
                    var paths = ProgramPaths(directory, suffixes, !isRecursiveSearch);
                    toFilterAllPaths.AddRange(paths);
                }
            }

            return toFilterAllPaths;
        }

        private static List<string> IndexPath(IList<string> suffixes, List<string> indexLocations)
             => indexLocations
                .SelectMany(indexLocation => ProgramPaths(indexLocation, suffixes))
                .ToList();

        private static List<string> StartMenuProgramPaths(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            var indexLocation = new List<string>() { directory1, directory2 };

            return IndexPath(suffixes, indexLocation);
        }

        private static List<string> DesktopProgramPaths(IList<string> suffixes)
        {
            var directory1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var directory2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            var indexLocation = new List<string>() { directory1, directory2 };

            return IndexPath(suffixes, indexLocation);
        }

        private static List<string> RegistryAppProgramPaths(IList<string> suffixes)
        {
            // https://msdn.microsoft.com/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            var paths = new List<string>();
            using (var root = Registry.LocalMachine.OpenSubKey(appPaths))
            {
                if (root != null)
                {
                    paths.AddRange(GetPathsFromRegistry(root));
                }
            }

            using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
            {
                if (root != null)
                {
                    paths.AddRange(GetPathsFromRegistry(root));
                }
            }

            return paths
                .Where(path => suffixes.Any(suffix => path.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase)))
                .Select(ExpandEnvironmentVariables)
                .ToList();
        }

        private static IEnumerable<string> GetPathsFromRegistry(RegistryKey root)
            => root
                .GetSubKeyNames()
                .Select(x => GetPathFromRegistrySubkey(root, x));

        private static string GetPathFromRegistrySubkey(RegistryKey root, string subkey)
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
                ProgramLogger.Warn($"|Permission denied when trying to load the program from {path}", e, MethodBase.GetCurrentMethod().DeclaringType, path);

                return string.Empty;
            }
        }

        private static string ExpandEnvironmentVariables(string path) =>
            path != null
                ? Environment.ExpandEnvironmentVariables(path)
                : null;

        // Overriding the object.GetHashCode() function to aid in removing duplicates while adding and removing apps from the concurrent dictionary storage
        public override int GetHashCode()
            => Win32ProgramEqualityComparer.Default.GetHashCode(this);

        public override bool Equals(object obj)
            => obj is Win32Program win32Program && Win32ProgramEqualityComparer.Default.Equals(this, win32Program);

        private class Win32ProgramEqualityComparer : IEqualityComparer<Win32Program>
        {
            public static readonly Win32ProgramEqualityComparer Default = new Win32ProgramEqualityComparer();

            public bool Equals(Win32Program app1, Win32Program app2)
            {
                if (app1 == null && app2 == null)
                {
                    return true;
                }

                return app1 != null
                       && app2 != null
                       && (app1.Name?.ToUpperInvariant(), app1.ExecutableName?.ToUpperInvariant(), app1.FullPath?.ToUpperInvariant())
                       .Equals((app2.Name?.ToUpperInvariant(), app2.ExecutableName?.ToUpperInvariant(), app2.FullPath?.ToUpperInvariant()));
            }

            public int GetHashCode(Win32Program obj)
                => (obj.Name?.ToUpperInvariant(), obj.ExecutableName?.ToUpperInvariant(), obj.FullPath?.ToUpperInvariant()).GetHashCode();
        }

        public static List<Win32Program> DeduplicatePrograms(IEnumerable<Win32Program> programs)
            => new HashSet<Win32Program>(programs, Win32ProgramEqualityComparer.Default).ToList();

        private static Win32Program GetProgramFromPath(string path)
        {
            var extension = Extension(path);
            if (ExecutableApplicationExtensions.Contains(extension))
            {
                return ExeProgram(path);
            }

            switch (extension)
            {
                case ShortcutExtension:
                    return LnkProgram(path);
                case ApplicationReferenceExtension:
                    return CreateWin32Program(path);
                case InternetShortcutExtension:
                    return InternetShortcutProgram(path);
                default:
                    return null;
            }
        }

        private static bool TryGetIcoPathForRunCommandProgram(Win32Program program, out string icoPath)
        {
            icoPath = null;

            if (program.AppType != ApplicationType.RunCommand)
            {
                return false;
            }

            if (string.IsNullOrEmpty(program.FullPath))
            {
                return false;
            }

            // https://msdn.microsoft.com/library/windows/desktop/ee872121
            try
            {
                var redirectionPath = ReparsePoint.GetTarget(program.FullPath);
                if (string.IsNullOrEmpty(redirectionPath))
                {
                    return false;
                }

                icoPath = ExpandEnvironmentVariables(redirectionPath);
                return true;
            }
            catch (IOException e)
            {
                ProgramLogger.Warn($"|Error whilst retrieving the redirection path from app execution alias {program.FullPath}", e, MethodBase.GetCurrentMethod().DeclaringType, program.FullPath);
            }

            icoPath = null;
            return false;
        }

        private static Win32Program GetRunCommandProgramFromPath(string path)
        {
            var program = GetProgramFromPath(path);
            program.AppType = ApplicationType.RunCommand;

            if (TryGetIcoPathForRunCommandProgram(program, out var icoPath))
            {
                program.IcoPath = icoPath;
            }

            return program;
        }

        public static IList<Win32Program> All(ProgramPluginSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            try
            {
                // Set an initial size to an expected size to prevent multiple hashSet resizes
                const int defaultHashsetSize = 1000;

                // Multiple paths could have the same programPaths and we don't want to resolve / lookup them multiple times
                var paths = new HashSet<string>(defaultHashsetSize);
                var runCommandPaths = new HashSet<string>(defaultHashsetSize);

                // Parallelize multiple sources, and priority based on paths which most likely contain .lnks which are formatted
                var sources = new (bool IsEnabled, Func<IEnumerable<string>> GetPaths)[]
                {
                    (true, () => CustomProgramPaths(settings.ProgramSources, settings.ProgramSuffixes)),
                    (settings.EnableStartMenuSource, () => StartMenuProgramPaths(settings.ProgramSuffixes)),
                    (settings.EnableDesktopSource, () => DesktopProgramPaths(settings.ProgramSuffixes)),
                    (settings.EnableRegistrySource, () => RegistryAppProgramPaths(settings.ProgramSuffixes)),
                };

                // Run commands are always set as AppType "RunCommand"
                var runCommandSources = new (bool IsEnabled, Func<IEnumerable<string>> GetPaths)[]
                {
                    (settings.EnablePathEnvironmentVariableSource, () => PathEnvironmentProgramPaths(settings.RunCommandSuffixes)),
                };

                var disabledProgramsList = settings.DisabledProgramSources;

                // Get all paths but exclude all normal .Executables
                paths.UnionWith(sources
                    .AsParallel()
                    .SelectMany(source => source.IsEnabled ? source.GetPaths() : Enumerable.Empty<string>())
                    .Where(programPath => disabledProgramsList.All(x => x.UniqueIdentifier != programPath))
                    .Where(path => !ExecutableApplicationExtensions.Contains(Extension(path))));
                runCommandPaths.UnionWith(runCommandSources
                    .AsParallel()
                    .SelectMany(source => source.IsEnabled ? source.GetPaths() : Enumerable.Empty<string>())
                    .Where(programPath => disabledProgramsList.All(x => x.UniqueIdentifier != programPath)));

                var programs = paths.AsParallel().Select(source => GetProgramFromPath(source));
                var runCommandPrograms = runCommandPaths.AsParallel().Select(source => GetRunCommandProgramFromPath(source));

                return DeduplicatePrograms(programs.Concat(runCommandPrograms).Where(program => program?.Valid == true));
            }
            catch (Exception e)
            {
                ProgramLogger.Exception("An unexpected error occurred", e, MethodBase.GetCurrentMethod().DeclaringType, "Not available");

                return Array.Empty<Win32Program>();
            }
        }
    }
}
