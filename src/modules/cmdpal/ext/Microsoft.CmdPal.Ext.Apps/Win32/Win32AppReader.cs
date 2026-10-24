// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Win32;

/// <summary>
/// Reads one executable, shortcut, application reference or supported game URL.
/// </summary>
internal static partial class Win32AppReader
{
    private const string ShortcutExtension = "lnk";
    private const string ApplicationReferenceExtension = "appref-ms";
    private const string InternetShortcutExtension = "url";
    private const string ProxyWebApp = "_proxy.exe";
    private const string AppIdArgument = "--app-id";
    private const string FirefoxExecutable = "firefox.exe";
    private const string FirefoxTaskbarTabArgument = "-taskbar-tab";

    private static readonly Win32AppMetadata InvalidMetadata = new() { Valid = false };
    private static readonly Regex InternetShortcutUrlPrefixes = InternetShortcutURLPrefixesGenerator();

    /// <summary>Reads desktop app or Run-command metadata using the requested source interpretation.</summary>
    /// <remarks>The returned metadata can describe an invalid candidate; discovery decides admission and retries.</remarks>
    internal static Win32AppMetadata LoadFromPath(string path, bool asRunCommand)
    {
        return asRunCommand ? ReadRunCommand(path) : ReadTarget(path);
    }

    private static Win32AppMetadata CreateMetadata(string path)
    {
        try
        {
            var parentDir = Directory.GetParent(path);

            return new Win32AppMetadata
            {
                Name = Path.GetFileNameWithoutExtension(path),
                SourceFilename = Path.GetFileName(path),
                IconLocation = path,

                TargetPath = path,
                ParentDirectory = parentDir is null ? string.Empty : parentDir.FullName,
                Valid = true,
                AppType = Win32AppType.Win32Application,

                DisplayName = ShellLocalization.Instance.GetLocalizedName(path),
                LocalizedTargetPath = ShellLocalization.Instance.GetLocalizedPath(path),
                LocalizedSourceFilename = Path.GetFileName(ShellLocalization.Instance.GetLocalizedPath(path)),
            };
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return RejectedMetadata(e);
        }
    }

    private static Win32AppMetadata ReadInternetShortcut(string path)
    {
        try
        {
            var lines = ReadShortcutLines(path);
            var iconPath = string.Empty;
            var urlPath = string.Empty;
            var validApp = false;

            const string urlPrefix = "URL=";
            const string iconFilePrefix = "IconFile=";

            foreach (var line in lines)
            {
                if (line.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    urlPath = line[urlPrefix.Length..];

                    if (!Uri.TryCreate(urlPath, UriKind.RelativeOrAbsolute, out _))
                    {
                        return InvalidMetadata;
                    }

                    if (InternetShortcutUrlPrefixes.IsMatch(urlPath))
                    {
                        validApp = true;
                    }
                }
                else if (line.StartsWith(iconFilePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    iconPath = line[iconFilePrefix.Length..];
                }

                // Stop once both fields are known; disposing the iterator releases the shared read handle.
                if (!string.IsNullOrEmpty(urlPath) && !string.IsNullOrEmpty(iconPath))
                {
                    break;
                }
            }

            if (!validApp)
            {
                return InvalidMetadata;
            }

            try
            {
                var parentDir = Directory.GetParent(path);

                return new Win32AppMetadata
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    SourceFilename = Path.GetFileName(path),
                    IconLocation = iconPath,
                    TargetPath = urlPath,
                    ParentDirectory = parentDir is null ? string.Empty : parentDir.FullName,
                    Valid = true,
                    AppType = Win32AppType.InternetShortcutApplication,
                };
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogError(e.Message);
                return InvalidMetadata;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return RejectedMetadata(e);
        }
    }

    private static IEnumerable<string> ReadShortcutLines(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static Win32AppMetadata RejectedMetadata(Exception exception)
    {
        if (PathHelpers.IsMissingOrInvalidPath(exception))
        {
            return InvalidMetadata;
        }

        var unreadable = exception is IOException or UnauthorizedAccessException or SecurityException
            || (exception is COMException && exception.HResult is
                unchecked((int)0x80070020) // ERROR_SHARING_VIOLATION
                or unchecked((int)0x80070021) // ERROR_LOCK_VIOLATION
                or unchecked((int)0x80070005) // ERROR_ACCESS_DENIED
                or unchecked((int)0x80030020) // STG_E_SHAREVIOLATION
                or unchecked((int)0x80030021) // STG_E_LOCKVIOLATION
                or unchecked((int)0x80030005)); // STG_E_ACCESSDENIED
        return unreadable
            ? new Win32AppMetadata { Valid = false, Unreadable = true, RetryableReadFailure = PathHelpers.IsRetryableReadFailure(exception) }
            : InvalidMetadata;
    }

    private static Win32AppMetadata ReadShortcut(string path)
    {
        try
        {
            var program = CreateMetadata(path);
            if (!program.Valid)
            {
                return program;
            }

            program.AppType = Win32AppType.ShortcutApplication;
            var link = ShellLinkReader.Read(path);
            if (link is null)
            {
                return InvalidMetadata;
            }

            var target = link.TargetPath;
            program.PackagedAppUserModelId = link.PackagedAppUserModelId;
            program.ExplicitAppUserModelId = link.ExplicitAppUserModelId;
            program.WorkingDirectory = link.WorkingDirectory;

            if (!string.IsNullOrEmpty(target))
            {
                if (!(File.Exists(target) || Directory.Exists(target)))
                {
                    // Keep the missing target so background validation can detect a later install.
                    program.TargetPath = target;
                    program.Valid = false;
                    return program;
                }

                program.LnkFilePath = program.TargetPath;
                program.TargetFilename = Path.GetFileName(target);
                program.LocalizedTargetFilename = Path.GetFileName(ShellLocalization.Instance.GetLocalizedPath(target));

                program.TargetPath = Path.GetFullPath(target);
                program.LocalizedTargetPath = ShellLocalization.Instance.GetLocalizedPath(target);

                program.Arguments = link.Arguments;

                // Browser-hosted web apps still launch through their original shortcuts.
                program.AppType = IsWebApplication(program)
                    ? Win32AppType.WebApplication
                    : GetAppTypeFromPath(target);

                var description = link.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    program.Description = description;
                }
                else
                {
                    program.Description = ReadFileDescription(target);
                }
            }

            program.IconLocation = !string.IsNullOrEmpty(link.IconLocation)
                ? link.IconLocation
                : program.TargetPath;

            return program;
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return RejectedMetadata(e);
        }
    }

    private static Win32AppMetadata ReadExecutable(string path)
    {
        try
        {
            var program = CreateMetadata(path);
            if (program.Valid)
            {
                program.Description = ReadFileDescription(path);
            }

            return program;
        }
        catch (Exception e)
        {
            Logger.LogError(e.Message);
            return RejectedMetadata(e);
        }
    }

    private static string ReadFileDescription(string path)
    {
        return File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileDescription ?? string.Empty : string.Empty;
    }

    private static Win32AppType GetAppTypeFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var extension = Extension(path);

        if (PathHelpers.IsExecutablePath(path))
        {
            return Win32AppType.Win32Application;
        }
        else if (extension.Equals(ShortcutExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Win32AppType.ShortcutApplication;
        }
        else if (extension.Equals(ApplicationReferenceExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Win32AppType.ApprefApplication;
        }
        else if (extension.Equals(InternetShortcutExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Win32AppType.InternetShortcutApplication;
        }
        else if (string.IsNullOrEmpty(extension) && Directory.Exists(path))
        {
            return Win32AppType.Folder;
        }

        return Win32AppType.GenericFile;
    }

    private static string Extension(string path)
    {
        var extension = Path.GetExtension(path)?.ToLowerInvariant();

        return !string.IsNullOrEmpty(extension)
            ? extension[1..]
            : string.Empty;
    }

    private static Win32AppMetadata ReadTarget(string path)
    {
        var extension = Extension(path);
        if (PathHelpers.IsExecutablePath(path))
        {
            return ReadExecutable(path);
        }

        return extension switch
        {
            ShortcutExtension => ReadShortcut(path),
            ApplicationReferenceExtension => CreateMetadata(path),
            InternetShortcutExtension => ReadInternetShortcut(path),
            _ => InvalidMetadata,
        };
    }

    private static ReparsePoint.AppExecutionAliasInfo? ReadExecutionAlias(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            var alias = ReparsePoint.GetAppExecutionAliasInfo(path);
            if (alias is null)
            {
                return null;
            }

            return alias with { TargetPath = string.IsNullOrEmpty(alias.TargetPath) ? string.Empty : Environment.ExpandEnvironmentVariables(alias.TargetPath) };
        }
        catch (IOException e)
        {
            Logger.LogError(e.Message);
        }

        return null;
    }

    private static Win32AppMetadata ReadRunCommand(string path)
    {
        var program = ReadTarget(path);
        if (!program.Valid)
        {
            return program;
        }

        program.AppType = Win32AppType.RunCommand;

        var appExecutionAlias = ReadExecutionAlias(program.TargetPath);
        program.AppExecutionAlias = appExecutionAlias;
        program.PackagedAppUserModelId = appExecutionAlias?.Aumid ?? string.Empty;
        if (!string.IsNullOrEmpty(appExecutionAlias?.TargetPath))
        {
            program.IconLocation = appExecutionAlias.TargetPath;
        }

        return program;
    }

    private static bool IsWebApplication(Win32AppMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.TargetPath) || string.IsNullOrEmpty(metadata.Arguments))
        {
            return false;
        }

        // Chromium PWA shortcuts launch the proxy executable with an application ID.
        if (metadata.TargetPath.Contains(ProxyWebApp, StringComparison.OrdinalIgnoreCase)
            && metadata.Arguments.Contains(AppIdArgument, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Path.GetFileName(metadata.TargetPath).Equals(FirefoxExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Match Firefox's web-app switch as a token, not text inside a URL or profile path.
        var arguments = CommandLineParser.ParseArguments(metadata.Arguments);
        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if ((arguments[i].Equals(FirefoxTaskbarTabArgument, StringComparison.OrdinalIgnoreCase)
                || arguments[i].Equals("-" + FirefoxTaskbarTabArgument, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrEmpty(arguments[i + 1])
                && !arguments[i + 1].StartsWith('-'))
            {
                return true;
            }
        }

        return false;
    }

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
}
