// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Security;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[Serializable]
public partial class Win32Program
{
    public static readonly Win32Program InvalidProgram = new() { Valid = false };

    private static readonly Regex InternetShortcutURLPrefixes = InternetShortcutURLPrefixesGenerator();

    private static readonly IFileSystem FileSystem = new FileSystem();
    private static readonly IPath Path = FileSystem.Path;
    private static readonly IFile File = FileSystem.File;
    private static readonly IDirectory Directory = FileSystem.Directory;

    public string Name { get; set; } = string.Empty;

    // Localized name based on windows display language
    public string NameLocalized { get; set; } = string.Empty;

    public string IcoPath { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Path of app executable or lnk target executable
    public string FullPath { get; set; } = string.Empty;

    // Localized path based on windows display language
    public string FullPathLocalized { get; set; } = string.Empty;

    public string ParentDirectory { get; set; } = string.Empty;

    public string ExecutableName { get; set; } = string.Empty;

    // Localized executable name based on windows display language
    public string ExecutableNameLocalized { get; set; } = string.Empty;

    // Path to the lnk file on LnkProgram
    public string LnkFilePath { get; set; } = string.Empty;

    public string LnkResolvedExecutableName { get; set; } = string.Empty;

    // Localized path based on windows display language
    public string LnkResolvedExecutableNameLocalized { get; set; } = string.Empty;

    public bool Valid { get; set; }

    public bool HasArguments => !string.IsNullOrEmpty(Arguments);

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets metadata resolved from this program when it is an app execution alias.
    /// </summary>
    internal ReparsePoint.AppExecutionAliasInfo? AppExecutionAlias { get; set; }

    /// <summary>Gets or sets the packaged application identity represented by this program.</summary>
    internal string PackagedAppUserModelId { get; set; } = string.Empty;

    public ApplicationType AppType { get; set; }

    // Wrappers for File Operations
    public static IFileVersionInfoWrapper FileVersionInfoWrapper { get; set; } = new FileVersionInfoWrapper();

    public static IFile FileWrapper { get; set; } = new FileSystem().File;

    private const string ShortcutExtension = "lnk";
    private const string ApplicationReferenceExtension = "appref-ms";
    private const string InternetShortcutExtension = "url";
    private static readonly HashSet<string> ExecutableApplicationExtensions = new(StringComparer.OrdinalIgnoreCase) { "exe", "bat", "bin", "com", "cpl", "msc", "msi", "cmd", "ps1", "job", "msp", "mst", "sct", "ws", "wsh", "wsf" };

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

        var subqueries = query?.Split() ?? [];
        var nameContainsQuery = false;
        var pathContainsQuery = false;

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

    public bool QueryEqualsNameForRunCommands(string query)
    {
        if (query is not null && AppType == ApplicationType.RunCommand)
        {
            // Using OrdinalIgnoreCase since this is used internally
            if (!query.Equals(Name, StringComparison.OrdinalIgnoreCase) && !query.Equals(ExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        return ExecutableName;
    }

    private static Win32Program CreateWin32Program(string path)
    {
        try
        {
            var parentDir = Directory.GetParent(path);

            return new Win32Program
            {
                Name = Path.GetFileNameWithoutExtension(path),
                ExecutableName = Path.GetFileName(path),
                IcoPath = path,

                // Using InvariantCulture since this is user facing
                FullPath = path,
                ParentDirectory = parentDir is null ? string.Empty : parentDir.FullName,
                Description = string.Empty,
                Valid = true,
                AppType = ApplicationType.Win32Application,

                // Localized name, path and executable based on windows display language
                NameLocalized = ShellLocalization.Instance.GetLocalizedName(path),
                FullPathLocalized = ShellLocalization.Instance.GetLocalizedPath(path),
                ExecutableNameLocalized = Path.GetFileName(ShellLocalization.Instance.GetLocalizedPath(path)),
            };
        }
        catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
    }

    // This function filters Internet Shortcut programs
    private static Win32Program InternetShortcutProgram(string path)
    {
        try
        {
            // We don't want to read the whole file if we don't need to
            var lines = FileWrapper.ReadLines(path);
            var iconPath = string.Empty;
            var urlPath = string.Empty;
            var validApp = false;

            const string urlPrefix = "URL=";
            const string iconFilePrefix = "IconFile=";

            foreach (var line in lines)
            {
                // Using OrdinalIgnoreCase since this is used internally
                if (line.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    urlPath = line.Substring(urlPrefix.Length);

                    if (!Uri.TryCreate(urlPath, UriKind.RelativeOrAbsolute, out var _))
                    {
                        return InvalidProgram;
                    }

                    // To filter out only those steam shortcuts which have 'run' or 'rungameid' as the hostname
                    if (InternetShortcutURLPrefixes.IsMatch(urlPath))
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
                var parentDir = Directory.GetParent(path);

                return new Win32Program
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ExecutableName = Path.GetFileName(path),
                    IcoPath = iconPath,
                    FullPath = urlPath,
                    ParentDirectory = parentDir is null ? string.Empty : parentDir.FullName,
                    Valid = true,
                    AppType = ApplicationType.InternetShortcutApplication,
                };
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogError(e.Message);
                return InvalidProgram;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
    }

    private static Win32Program LnkProgram(string path)
    {
        try
        {
            var program = CreateWin32Program(path);
            program.AppType = ApplicationType.ShortcutApplication;
            var link = ShellLinkReader.Read(path);
            if (link is null)
            {
                return InvalidProgram;
            }

            var target = link.TargetPath;
            program.PackagedAppUserModelId = link.PackagedAppUserModelId;
            program.ExplicitAppUserModelId = link.ExplicitAppUserModelId;
            program.WorkingDirectory = link.WorkingDirectory;

            if (!string.IsNullOrEmpty(target))
            {
                if (!(File.Exists(target) || Directory.Exists(target)))
                {
                    // If the link points nowhere, consider it invalid.
                    return InvalidProgram;
                }

                program.LnkFilePath = program.FullPath;
                program.LnkResolvedExecutableName = Path.GetFileName(target);
                program.LnkResolvedExecutableNameLocalized = Path.GetFileName(ShellLocalization.Instance.GetLocalizedPath(target));

                // Using CurrentCulture since this is user facing
                program.FullPath = Path.GetFullPath(target);
                program.FullPathLocalized = ShellLocalization.Instance.GetLocalizedPath(target);

                program.Arguments = link.Arguments;

                // A .lnk could be a (Chrome) PWA, set correct AppType
                program.AppType = program.IsWebApplication()
                    ? ApplicationType.WebApplication
                    : GetAppTypeFromPath(target);

                var description = link.Description;
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

            program.IcoPath = !string.IsNullOrEmpty(link.IconLocation)
                ? link.IconLocation
                : program.FullPath;

            return program;
        }
        catch (System.IO.FileLoadException e)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }

        // Only do a catch all in production. This is so make developer aware of any unhandled exception and add the exception handling in.
        // Error caused likely due to trying to get the description of the program
        catch (Exception e)
        {
            Logger.LogError(e.Message);
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
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
        catch (FileNotFoundException e)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return InvalidProgram;
        }
    }

    // Function to get the application type, given the path to the application
    public static ApplicationType GetAppTypeFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var extension = Extension(path);

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
        else if (string.IsNullOrEmpty(extension) && System.IO.Directory.Exists(path))
        {
            return ApplicationType.Folder;
        }

        return ApplicationType.GenericFile;
    }

    // Function to get the Win32 application, given the path to the application
    public static Win32Program? GetAppFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Win32Program? app;
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

    private static IEnumerable<string> ProgramPaths(string directory, IList<string> suffixes, int maximumDepth = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = new List<string>();
        var folderQueue = new Queue<(string Path, int Depth)>();
        folderQueue.Enqueue((directory, 0));

        // Keep track of already visited directories to avoid cycles.
        var alreadyVisited = new HashSet<string>();

        do
        {
            var current = folderQueue.Dequeue();
            var currentDirectory = current.Path;

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
                        Logger.LogError(e.Message);
                    }
                }
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogError(e.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }

            try
            {
                if (current.Depth >= maximumDepth)
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
                    folderQueue.Enqueue((childDirectory, current.Depth + 1));
                }
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogError(e.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }
        while (folderQueue.Count > 0);

        return files;
    }

    internal static IEnumerable<string> EnumerateProgramPaths(string directory, IList<string> suffixes, int maximumDepth = int.MaxValue)
        => ProgramPaths(directory, suffixes, maximumDepth);

    private static string Extension(string path)
    {
        // Using InvariantCulture since this is user facing
        var extension = Path.GetExtension(path)?.ToLowerInvariant();

        return !string.IsNullOrEmpty(extension)
            ? extension.Substring(1)
            : string.Empty;
    }

    /// <summary>Determines whether a source path directly names a supported executable type.</summary>
    internal static bool IsExecutablePath(string path)
        => ExecutableApplicationExtensions.Contains(Extension(path));

    /// <summary>Determines whether an application type uses its executable target as catalog identity.</summary>
    internal static bool UsesExecutableTargetIdentity(ApplicationType appType)
        => appType is ApplicationType.Win32Application
            or ApplicationType.RunCommand
            or ApplicationType.WebApplication;

    /// <summary>Normalizes launch profiles without distinguishing default executable or installer directories.</summary>
    internal static string GetDistinctWorkingDirectory(string targetPath, string workingDirectory, string? explicitAppUserModelId = null)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return string.Empty;
        }

        var expandedDirectory = Environment.ExpandEnvironmentVariables(workingDirectory);
        if (!Path.IsPathFullyQualified(expandedDirectory))
        {
            return expandedDirectory;
        }

        var normalizedDirectory = PathHelpers.NormalizePath(expandedDirectory);
        var expandedTarget = Environment.ExpandEnvironmentVariables(targetPath);
        if (Path.IsPathFullyQualified(expandedDirectory) && Path.IsPathFullyQualified(expandedTarget))
        {
            try
            {
                var targetDirectory = PathHelpers.NormalizePath(Path.GetDirectoryName(expandedTarget)!);
                if (IsExecutablePath(expandedTarget)
                    && (string.Equals(normalizedDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase)
                        || IsSquirrelVersionDirectory(expandedTarget, targetDirectory, normalizedDirectory, explicitAppUserModelId)))
                {
                    return string.Empty;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Keep malformed launch profiles distinct.
            }
        }

        return normalizedDirectory;
    }

    // Function to obtain the list of applications, the locations of which have been added to the env variable PATH
    private static List<string> PathEnvironmentProgramPaths(IList<string> suffixes)
    {
        // To get all the locations stored in the PATH env variable
        var pathEnvVariable = Environment.GetEnvironmentVariable("PATH");
        var searchPaths = pathEnvVariable?.Split(Path.PathSeparator);
        var toFilterAllPaths = new List<string>();
        if (searchPaths is not null)
        {
            foreach (var path in searchPaths)
            {
                if (path.Length > 0)
                {
                    // to expand any environment variables present in the path
                    var directory = Environment.ExpandEnvironmentVariables(path);
                    var paths = ProgramPaths(directory, suffixes, maximumDepth: 0);
                    toFilterAllPaths.AddRange(paths);
                }
            }
        }

        return toFilterAllPaths;
    }

    internal static IEnumerable<string> EnumeratePathEnvironmentPrograms(IList<string> suffixes)
        => PathEnvironmentProgramPaths(suffixes);

    private static List<(string CommandName, string TargetPath)> RegistryAppPrograms(IList<string> suffixes)
    {
        // https://msdn.microsoft.com/library/windows/desktop/ee872121
        const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        var programs = new List<(string CommandName, string TargetPath)>();
        using (var root = Registry.LocalMachine.OpenSubKey(appPaths))
        {
            if (root is not null)
            {
                programs.AddRange(GetProgramsFromRegistry(root));
            }
        }

        using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
        {
            if (root is not null)
            {
                programs.AddRange(GetProgramsFromRegistry(root));
            }
        }

        var returnedPrograms = new List<(string CommandName, string TargetPath)>();
        foreach (var program in programs)
        {
            var matchesSuffix = false;
            foreach (var suffix in suffixes)
            {
                if (program.TargetPath.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    matchesSuffix = true;
                    break;
                }
            }

            if (matchesSuffix)
            {
                var expandedPath = ExpandEnvironmentVariables(program.TargetPath);
                if (expandedPath is not null)
                {
                    returnedPrograms.Add((program.CommandName, expandedPath));
                }
            }
        }

        return returnedPrograms;
    }

    internal static IEnumerable<(string CommandName, string TargetPath)> EnumerateRegistryPrograms(IList<string> suffixes)
    {
        return RegistryAppPrograms(suffixes);
    }

    private static IEnumerable<(string CommandName, string TargetPath)> GetProgramsFromRegistry(RegistryKey root)
    {
        var result = new List<(string CommandName, string TargetPath)>();

        // Get all subkey names
        var subKeyNames = root.GetSubKeyNames();

        // Process each subkey to extract the path
        foreach (var subkeyName in subKeyNames)
        {
            var path = GetPathFromRegistrySubkey(root, subkeyName);
            if (!string.IsNullOrEmpty(path))
            {
                result.Add((subkeyName, path));
            }
        }

        return result;
    }

    private static string GetPathFromRegistrySubkey(RegistryKey root, string subkey)
    {
        var path = string.Empty;
        try
        {
            using (var key = root.OpenSubKey(subkey))
            {
                if (key is null)
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
            Logger.LogError(e.Message);
            return string.Empty;
        }
    }

    private static string ExpandEnvironmentVariables(string path) =>
        !string.IsNullOrEmpty(path)
            ? Environment.ExpandEnvironmentVariables(path)
            : string.Empty;

    // Overriding the object.GetHashCode() function to aid in removing duplicates while adding and removing apps from the concurrent dictionary storage
    public override int GetHashCode()
        => Win32ProgramEqualityComparer.Default.GetHashCode(this);

    public override bool Equals(object? obj)
        => obj is Win32Program win32Program && Win32ProgramEqualityComparer.Default.Equals(this, win32Program);

    public static List<Win32Program> DeduplicatePrograms(IEnumerable<Win32Program> programs)
    {
        // Create a HashSet with the custom equality comparer to automatically deduplicate programs
        var uniquePrograms = new HashSet<Win32Program>(Win32ProgramEqualityComparer.Default);

        // Filter out invalid programs and add valid ones to the HashSet
        foreach (var program in programs)
        {
            if (program?.Valid == true)
            {
                uniquePrograms.Add(program);
            }
        }

        // Convert the HashSet to a List for return
        var result = new List<Win32Program>(uniquePrograms.Count);
        foreach (var program in uniquePrograms)
        {
            result.Add(program);
        }

        return result;
    }

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
                return InvalidProgram;
        }
    }

    private static ReparsePoint.AppExecutionAliasInfo? GetAppExecutionAliasInfoForRunCommandProgram(Win32Program program)
    {
        if (program.AppType != ApplicationType.RunCommand)
        {
            return null;
        }

        if (string.IsNullOrEmpty(program.FullPath))
        {
            return null;
        }

        // https://msdn.microsoft.com/library/windows/desktop/ee872121
        try
        {
            var alias = ReparsePoint.GetAppExecutionAliasInfo(program.FullPath);
            if (alias is null)
            {
                return null;
            }

            return alias with { TargetPath = ExpandEnvironmentVariables(alias.TargetPath) };
        }
        catch (IOException e)
        {
            Logger.LogError(e.Message);
        }

        return null;
    }

    private static Win32Program GetRunCommandProgramFromPath(string path)
    {
        var program = GetProgramFromPath(path);
        if (program.Valid)
        {
            program.AppType = ApplicationType.RunCommand;

            var appExecutionAlias = GetAppExecutionAliasInfoForRunCommandProgram(program);
            program.AppExecutionAlias = appExecutionAlias;
            program.PackagedAppUserModelId = appExecutionAlias?.Aumid ?? string.Empty;
            if (!string.IsNullOrEmpty(appExecutionAlias?.TargetPath))
            {
                program.IcoPath = appExecutionAlias.TargetPath;
            }
        }

        return program;
    }

    internal static Win32Program LoadFromPath(string path, bool asRunCommand)
        => asRunCommand ? GetRunCommandProgramFromPath(path) : GetProgramFromPath(path);

    [GeneratedRegex(
        """
        (?:
            ^steam://(?:rungameid|run|open)/
            |
            ^com\.epicgames\.launcher://apps/
            |
            ^origin2?://game/
            |
            ^link2ea://launchgame/
            |
            ^uplay://launch/
            |
            ^msgamelaunch://shortcutLaunch/
        )
        """,
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex InternetShortcutURLPrefixesGenerator();

    private sealed class Win32ProgramEqualityComparer : IEqualityComparer<Win32Program>
    {
        public static readonly Win32ProgramEqualityComparer Default = new();

        public bool Equals(Win32Program? app1, Win32Program? app2)
        {
            if (app1 is null && app2 is null)
            {
                return true;
            }

            if (app1 is null || app2 is null)
            {
                return false;
            }

            return string.Equals(app1.Name, app2.Name, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(app1.ExecutableName, app2.ExecutableName, StringComparison.OrdinalIgnoreCase)
                   && app1.Target.Equals(app2.Target)
                   && string.Equals(app1.Arguments, app2.Arguments, StringComparison.Ordinal)
                   && string.Equals(
                       GetDistinctWorkingDirectory(app1.AppExecutionAlias?.TargetPath ?? app1.FullPath, app1.WorkingDirectory, app1.ExplicitAppUserModelId),
                       GetDistinctWorkingDirectory(app2.AppExecutionAlias?.TargetPath ?? app2.FullPath, app2.WorkingDirectory, app2.ExplicitAppUserModelId),
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Win32Program app)
        {
            HashCode hash = default;
            hash.Add(app.Name, StringComparer.OrdinalIgnoreCase);
            hash.Add(app.ExecutableName, StringComparer.OrdinalIgnoreCase);
            hash.Add(app.Target);
            hash.Add(app.Arguments, StringComparer.Ordinal);
            hash.Add(GetDistinctWorkingDirectory(app.AppExecutionAlias?.TargetPath ?? app.FullPath, app.WorkingDirectory, app.ExplicitAppUserModelId), StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }

    /// <summary>Gets or sets the explicit Windows application ID declared by this shortcut.</summary>
    internal string ExplicitAppUserModelId { get; set; } = string.Empty;

    private static bool IsSquirrelVersionDirectory(string targetPath, string targetDirectory, string workingDirectory, string? explicitAppUserModelId)
    {
        if (string.IsNullOrEmpty(explicitAppUserModelId)
            || !string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(workingDirectory), targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // ponytail: numeric release folders only; extend parsing when prerelease shortcuts need deduplication.
        var directoryName = Path.GetFileName(workingDirectory);
        if (!directoryName.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
            || !Version.TryParse(directoryName.AsSpan(4), out _))
        {
            return false;
        }

        // Squirrel's root launcher chooses the installed version and its working directory itself.
        var expectedId = $"com.squirrel.{Path.GetFileName(targetDirectory).Replace(" ", string.Empty)}.{Path.GetFileNameWithoutExtension(targetPath).Replace(" ", string.Empty)}";
        return string.Equals(explicitAppUserModelId, expectedId, StringComparison.OrdinalIgnoreCase);
    }

    internal LaunchTarget Target => AppType == ApplicationType.InternetShortcutApplication
        ? LaunchTarget.Url(FullPath)
        : LaunchTarget.FilePath(FullPath);
}
