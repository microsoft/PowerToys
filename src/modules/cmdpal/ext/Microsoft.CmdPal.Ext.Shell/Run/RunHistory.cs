// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CmdPal.Core.Common.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// Pure C# implementation of Run history functionality.
/// This is a port of the C++ RunHistory class from SystemIndexerNative.
/// </summary>
public static class RunHistory
{
    private const int MaxPath = 260;

    /// <summary>
    /// Executes a command line, optionally as administrator.
    /// This mirrors the behavior of the C++ RunDlg_OkPushed function.
    /// </summary>
    /// <param name="commandLine">The command line to execute.</param>
    /// <param name="workingDirectory">The working directory for the command.</param>
    /// <param name="hwnd">The parent window handle.</param>
    /// <param name="runAsAdmin">Whether to run elevated.</param>
    /// <returns>An HRESULT indicating success (0) or failure.</returns>
    public static int ExecuteCommandline(
        string commandLine,
        string workingDirectory,
        ulong hwnd,
        bool runAsAdmin)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return unchecked((int)0x80070057); // E_INVALIDARG
        }

        // Expand environment variables (like SHExpandEnvironmentStrings)
        var expandedCommand = Environment.ExpandEnvironmentVariables(commandLine);

        return ShellExecCmdLine(
            new HWND((nint)hwnd),
            expandedCommand,
            workingDirectory,
            runAsAdmin);
    }

    /// <summary>
    /// Qualifies the working directory for a command line based on the file path.
    /// This mirrors the behavior of the C++ _QualifyWorkingDir function.
    /// </summary>
    /// <param name="commandLine">The original command line.</param>
    /// <param name="filePath">The resolved file path (executable or URL).</param>
    /// <param name="defaultDirectory">The default directory to use if qualification is not needed.</param>
    /// <returns>The qualified working directory.</returns>
    /// <remarks>
    /// The logic is:
    /// - If the file path is a URL, return the default directory.
    /// - If the command line contains a backslash or colon (indicating path info),
    ///   return the directory containing the file.
    /// - Otherwise, return the default directory.
    /// </remarks>
    public static string QualifyCommandLineDirectory(
        string commandLine,
        string filePath,
        string defaultDirectory)
    {
        // If it's a URL, don't qualify
        if (Uri.IsWellFormedUriString(filePath, UriKind.Absolute))
        {
            return defaultDirectory;
        }

        // Check if we should qualify based on the original command line.
        // We qualify if the command line contains path information (backslash or colon).
        var hasBackslash = commandLine.Contains('\\');
        var hasColon = commandLine.Contains(':');

        if (!hasBackslash && !hasColon)
        {
            // No path information in the command line, use default directory
            return defaultDirectory;
        }

        // Qualify: get the directory from the file path
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }
        catch (ArgumentException)
        {
            // Path.GetDirectoryName can throw for invalid paths
        }

        return defaultDirectory;
    }

    /// <summary>
    /// Parses a command line into its file path and arguments components.
    /// This mirrors the behavior of SHEvaluateSystemCommandTemplate and
    /// _EvaluateUserCommandLine from the C++ implementation.
    /// </summary>
    /// <param name="commandLine">The command line to parse.</param>
    /// <param name="workingDirectory">The working directory context.</param>
    /// <returns>A ParseCommandlineResult with the parsed components.</returns>
    public static ParseCommandlineResult ParseCommandline(
        string commandLine,
        string workingDirectory)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return new ParseCommandlineResult
            {
                Result = unchecked((int)0x80070057), // E_INVALIDARG
                IsUri = false,
                FilePath = string.Empty,
                Arguments = string.Empty,
            };
        }

        // Check if it's a URL
        if (IsUrl(commandLine))
        {
            return new ParseCommandlineResult
            {
                Result = 0,
                IsUri = true,
                FilePath = commandLine,
                Arguments = string.Empty,
            };
        }

        // Try using SHEvaluateSystemCommandTemplate first
        var result = TryEvaluateSystemCommandTemplate(commandLine, out var filePath, out var arguments);

        if (result == 0)
        {
            return new ParseCommandlineResult
            {
                Result = 0,
                IsUri = false,
                FilePath = filePath,
                Arguments = arguments,
            };
        }

        // Fall back to user command line evaluation (like _EvaluateUserCommandLine)
        result = EvaluateUserCommandLine(commandLine, workingDirectory, out filePath, out arguments);

        return new ParseCommandlineResult
        {
            Result = result,
            IsUri = false,
            FilePath = filePath,
            Arguments = arguments,
        };
    }

    /// <summary>
    /// Executes a command using ShellExecuteEx.
    /// This is a simplified port of ShellExecCmdLineWithSite.
    /// </summary>
    private static unsafe int ShellExecCmdLine(
        HWND hwnd,
        string command,
        string? workingDirectory,
        bool runAsAdmin)
    {
        // Parse the command to get file and args
        var parseResult = ParseCommandline(command, workingDirectory ?? string.Empty);

        if (parseResult.Result != 0)
        {
            return parseResult.Result;
        }

        var filePath = parseResult.FilePath;
        var arguments = parseResult.Arguments;

        // Qualify the working directory
        var qualifiedWorkingDir = QualifyCommandLineDirectory(
            command,
            filePath,
            workingDirectory ?? string.Empty);

        // Setup ShellExecuteEx
        fixed (char* filePtr = filePath)
        {
            fixed (char* argsPtr = string.IsNullOrEmpty(arguments) ? null : arguments)
            {
                fixed (char* dirPtr = string.IsNullOrEmpty(qualifiedWorkingDir) ? null : qualifiedWorkingDir)
                {
                    fixed (char* verbPtr = runAsAdmin ? "runas" : null)
                    {
                        var info = new SHELLEXECUTEINFOW
                        {
                            cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                            hwnd = hwnd,
                            lpFile = filePtr,
                            lpParameters = argsPtr,
                            lpDirectory = dirPtr,
                            lpVerb = verbPtr,
                            nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL,
                            fMask = PInvoke.SEE_MASK_NOASYNC |
                                    PInvoke.SEE_MASK_DOENVSUBST |
                                    PInvoke.SEE_MASK_FLAG_LOG_USAGE |
                                    PInvoke.SEE_MASK_INVOKEIDLIST,
                        };

                        if (PInvoke.ShellExecuteEx(ref info))
                        {
                            return 0; // S_OK
                        }

                        // Return the last Win32 error as an HRESULT
                        var error = Marshal.GetLastWin32Error();
                        return error == 0 ? unchecked((int)0x80004005) : unchecked((int)(0x80070000 | (uint)error));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a string is a URL.
    /// This mirrors UrlIs(pszCommand, URLIS_URL) from shlwapi.
    /// </summary>
    private static bool IsUrl(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Check for common URL schemes
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Use Uri.TryCreate for more comprehensive URL detection
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var scheme = uri.Scheme;

            // Reject file paths that got parsed as URIs (like "C:\path")
            if (scheme.Length == 1 && char.IsLetter(scheme[0]))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to use SHEvaluateSystemCommandTemplate to parse a command.
    /// </summary>
    private static unsafe int TryEvaluateSystemCommandTemplate(
        string command,
        out string filePath,
        out string arguments)
    {
        filePath = string.Empty;
        arguments = string.Empty;

        PWSTR pszFile = default;
        PWSTR pszArgs = default;

        try
        {
            var hr = PInvoke.SHEvaluateSystemCommandTemplate(command, out pszFile, out var _, out pszArgs);
            if (hr.Failed)
            {
                return hr.Value;
            }

            filePath = new string(pszFile);
            arguments = pszArgs.Value != null ? new string(pszArgs) : string.Empty;

            return 0;
        }
        finally
        {
            if (pszFile.Value != null)
            {
                PInvoke.CoTaskMemFree(pszFile.Value);
            }

            if (pszArgs.Value != null)
            {
                PInvoke.CoTaskMemFree(pszArgs.Value);
            }
        }
    }

    /// <summary>
    /// Fallback command line parsing when SHEvaluateSystemCommandTemplate fails.
    /// This mirrors _EvaluateUserCommandLine from the C++ implementation.
    /// </summary>
    private static unsafe int EvaluateUserCommandLine(
        string command,
        string workingDirectory,
        out string filePath,
        out string arguments)
    {
        filePath = string.Empty;
        arguments = string.Empty;

        if (string.IsNullOrEmpty(command))
        {
            return unchecked((int)0x80070057); // E_INVALIDARG
        }

        // If the command is quoted, extract the quoted portion as the file
        if (command[0] == '"')
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 1)
            {
                filePath = command.Substring(1, endQuote - 1);
                if (endQuote + 1 < command.Length)
                {
                    arguments = command.Substring(endQuote + 1).TrimStart();
                }

                return 0;
            }
        }

        // Use PathGetArgs equivalent - find the first space that separates file from args
        // But first, handle the case where the file path might contain spaces
        // Try to find an executable by progressively adding more of the command
        var parts = command.Split(' ', StringSplitOptions.None);

        // Try to find an executable starting from fewer parts
        var candidatePath = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                candidatePath.Append(' ');
            }

            candidatePath.Append(parts[i]);

            var candidate = candidatePath.ToString();

            // Check if this candidate exists as a file or can be resolved
            if (TryResolveExecutable(candidate, out var resolved))
            {
                filePath = resolved;

                // The rest are arguments - build manually to avoid LINQ
                if (i + 1 < parts.Length)
                {
                    var argsBuilder = new StringBuilder();
                    for (var j = i + 1; j < parts.Length; j++)
                    {
                        if (j > i + 1)
                        {
                            argsBuilder.Append(' ');
                        }

                        argsBuilder.Append(parts[j]);
                    }

                    arguments = argsBuilder.ToString();
                }

                return 0;
            }
        }

        // If we couldn't resolve anything, just split at first space
        var firstSpace = command.IndexOf(' ');
        if (firstSpace > 0)
        {
            filePath = command.Substring(0, firstSpace);
            arguments = command.Substring(firstSpace + 1);
        }
        else
        {
            filePath = command;
        }

        return 0;
    }

    /// <summary>
    /// Tries to resolve an executable path using SearchPathW.
    /// </summary>
    private static bool TryResolveExecutable(string name, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // First check if the file exists directly
        if (File.Exists(name))
        {
            resolvedPath = Path.GetFullPath(name);
            return true;
        }

        // Try with .exe extension
        var withExe = name + ".exe";
        if (File.Exists(withExe))
        {
            resolvedPath = Path.GetFullPath(withExe);
            return true;
        }

        // Use SearchPath to find in PATH
        var buffer = new char[MaxPath];
        var result = PInvoke.SearchPath(null, name, ".exe", buffer);

        if (result > 0 && result <= buffer.Length)
        {
            resolvedPath = new string(buffer, 0, (int)result);
            return true;
        }

        return false;
    }
}
