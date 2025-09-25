// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

/// <summary>
/// Provides command line normalization functionality compatible with .NET
/// Native AOT. This is a C# port of the Profile::NormalizeCommandLine function
/// from the Windows Terminal codebase.
///
/// It was ported from 7055b99ac on 2025-09-25
/// </summary>
public static class CommandLineNormalizer
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int MAX_PATH = 260;
    private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
#pragma warning restore SA1310 // Field names should not contain underscore

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExpandEnvironmentStringsW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpSrc,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpDst,
        uint nSize);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
        out int pNumArgs);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint SearchPathW(
        [MarshalAs(UnmanagedType.LPWStr)] string? lpPath,
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpExtension,
        uint nBufferLength,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpBuffer,
        out IntPtr lpFilePart);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFileAttributesW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>
    /// Normalizes a command line string by expanding environment variables, resolving executable paths,
    /// and standardizing the format for comparison purposes.
    /// </summary>
    /// <param name="commandLine">The command line string to normalize</param>
    /// <returns>A normalized command line string</returns>
    /// <remarks>
    /// This function performs the following operations:
    /// 1. Expands environment variables (e.g., %SystemRoot% -> C:\WINDOWS)
    /// 2. Parses the command line into arguments, stripping quotes
    /// 3. Resolves the executable path to an absolute, canonical path
    /// 4. Reconstructs the command line with null separators between arguments
    ///
    /// Given a commandLine like:
    /// * "C:\WINDOWS\System32\cmd.exe"
    /// * "pwsh -WorkingDirectory ~"
    /// * "C:\Program Files\PowerShell\7\pwsh.exe"
    /// * "C:\Program Files\PowerShell\7\pwsh.exe -WorkingDirectory ~"
    ///
    /// This function returns:
    /// * "C:\Windows\System32\cmd.exe"
    /// * "C:\Program Files\PowerShell\7\pwsh.exe\0-WorkingDirectory\0~"
    /// * "C:\Program Files\PowerShell\7\pwsh.exe"
    /// * "C:\Program Files\PowerShell\7\pwsh.exe\0-WorkingDirectory\0~"
    ///
    /// The resulting strings are used for comparisons in profile matching.
    /// </remarks>
    public static string NormalizeCommandLine(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return string.Empty;
        }

        // Turn "%SystemRoot%\System32\cmd.exe" into "C:\WINDOWS\System32\cmd.exe".
        // We do this early, as environment variables might occur anywhere in the commandLine.
        var normalized = ExpandEnvironmentVariables(commandLine);

        // One of the most important things this function does is to strip quotes.
        // That way the commandLine "foo.exe -bar" and "\"foo.exe\" \"-bar\"" appear identical.
        // We'll use CommandLineToArgvW for that as it's close to what CreateProcessW uses.
        var argv = ParseCommandLineToArguments(normalized);

        if (argv.Length == 0)
        {
            return normalized;
        }

        // The index of the first argument in argv after our executable in argv[0].
        // Given {"C:\Program Files\PowerShell\7\pwsh.exe", "-WorkingDirectory", "~"} this will be 1.
        var startOfArguments = 1;

        // The given commandLine should start with an executable name or path.
        // This loop tries to resolve relative paths, as well as executable names in %PATH%
        // into absolute paths and normalizes them.
        var executablePath = ResolveExecutablePath(argv, ref startOfArguments);

        // We've (hopefully) finished resolving the path to the executable.
        // We're now going to append all remaining arguments to the resulting string.
        // If argv is {"C:\Program Files\PowerShell\7\pwsh.exe", "-WorkingDirectory", "~"},
        // then we'll get "C:\Program Files\PowerShell\7\pwsh.exe\0-WorkingDirectory\0~"
        var result = new StringBuilder(executablePath);

        for (var i = startOfArguments; i < argv.Length; i++)
        {
            result.Append('\0');
            result.Append(argv[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Expands environment variables in a string using Windows API.
    /// </summary>
    private static string ExpandEnvironmentVariables(string input)
    {
        const int initialBufferSize = 1024;
        var buffer = new StringBuilder(initialBufferSize);

        var result = ExpandEnvironmentStringsW(input, buffer, (uint)buffer.Capacity);

        if (result == 0)
        {
            // Failed to expand, return original string
            return input;
        }

        if (result > buffer.Capacity)
        {
            // Buffer was too small, resize and try again
            buffer.Capacity = (int)result;
            result = ExpandEnvironmentStringsW(input, buffer, (uint)buffer.Capacity);

            if (result == 0)
            {
                return input;
            }
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Parses a command line string into arguments using CommandLineToArgvW.
    /// </summary>
    private static string[] ParseCommandLineToArguments(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var argc);

        if (argv == IntPtr.Zero || argc == 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var args = new string[argc];

            for (var i = 0; i < argc; i++)
            {
                var argPtr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                args[i] = Marshal.PtrToStringUni(argPtr) ?? string.Empty;
            }

            return args;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    /// <summary>
    /// Resolves the executable path from the command line arguments.
    /// Handles cases where the path contains spaces and was split during parsing.
    /// </summary>
    private static string ResolveExecutablePath(string[] argv, ref int startOfArguments)
    {
        if (argv.Length == 0)
        {
            return string.Empty;
        }

        // Try to resolve the executable path, handling cases where spaces in paths
        // might have caused the path to be split across multiple arguments
        for (var pathLength = 1; pathLength <= argv.Length; pathLength++)
        {
            // Build potential executable path by combining arguments
            var pathBuilder = new StringBuilder(argv[0]);
            for (var i = 1; i < pathLength; i++)
            {
                pathBuilder.Append(' ');
                pathBuilder.Append(argv[i]);
            }

            var candidatePath = pathBuilder.ToString();
            var resolvedPath = TryResolveExecutable(candidatePath);

            if (!string.IsNullOrEmpty(resolvedPath))
            {
                startOfArguments = pathLength;
                return GetCanonicalPath(resolvedPath);
            }
        }

        // If we couldn't resolve the path, return the first argument as-is
        startOfArguments = 1;
        return argv[0];
    }

    /// <summary>
    /// Attempts to resolve an executable path using SearchPathW.
    /// </summary>
    private static string TryResolveExecutable(string executableName)
    {
        var buffer = new StringBuilder(MAX_PATH);

        var result = SearchPathW(
            null,           // Use default search path
            executableName,
            ".exe",         // Default extension
            (uint)buffer.Capacity,
            buffer,
            out var _);   // We don't need the file part

        if (result == 0)
        {
            return string.Empty;
        }

        if (result > buffer.Capacity)
        {
            // Buffer was too small, resize and try again
            buffer.Capacity = (int)result;
            result = SearchPathW(null, executableName, ".exe", (uint)buffer.Capacity, buffer, out var _);

            if (result == 0)
            {
                return string.Empty;
            }
        }

        var resolvedPath = buffer.ToString();

        // Verify the resolved path exists and is not a directory
        var attributes = GetFileAttributesW(resolvedPath);

        return attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0 ? string.Empty : resolvedPath;
    }

    /// <summary>
    /// Gets the canonical (absolute, normalized) path for a file.
    /// </summary>
    private static string GetCanonicalPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // If canonicalization fails, return the original path
            return path;
        }
    }
}
