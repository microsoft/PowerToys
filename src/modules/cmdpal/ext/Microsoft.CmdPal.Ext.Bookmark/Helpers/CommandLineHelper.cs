// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

/// <summary>
/// Provides helper methods for parsing command lines and expanding paths.
/// </summary>
/// <remarks>
/// Warning: This code handles parsing specifically for Bookmarks, and is NOT a general-purpose command line parser.
/// In some cases it mimics system rules (e.g. CreateProcess, CommandLineToArgvW) but in other cases it uses, but it can also
/// bend the rules to be more forgiving.
/// </remarks>
internal static partial class CommandLineHelper
{
    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static string[] SplitCommandLine(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var argv = NativeMethods.CommandLineToArgvW(commandLine, out var argc);
        if (argv == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var result = new string[argc];
            for (var i = 0; i < argc; i++)
            {
                var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                result[i] = Marshal.PtrToStringUni(p)!;
            }

            return result;
        }
        finally
        {
            NativeMethods.LocalFree(argv);
        }
    }

    /// <summary>
    /// Splits the raw command line into the first argument (Head) and the remainder (Tail). This method follows the rules
    /// of CommandLineToArgvW.
    /// </summary>
    /// <remarks>
    /// This is a mental support for SplitLongestHeadBeforeQuotedArg.
    ///
    /// Rules:
    /// - If the input starts with any whitespace, Head is an empty string (per CommandLineToArgvW behavior for first segment, handles by CreateProcess rules).
    /// - Otherwise, Head uses the CreateProcess "program name" rule:
    ///     - If the first char is a quote, Head is everything up to the next quote (backslashes do NOT escape it).
    ///     - Else, Head is the run up to the first whitespace.
    /// - Tail starts at the first non-whitespace character after Head (or is empty if nothing remains).
    /// No normalization is performed; returned slices preserve the original text (no un/escaping).
    /// </remarks>
    public static (string Head, string Tail) SplitHeadAndArgs(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var s = input.AsSpan();
        var n = s.Length;
        var i = 0;

        // Leading whitespace -> empty argv[0]
        if (char.IsWhiteSpace(s[0]))
        {
            while (i < n && char.IsWhiteSpace(s[i]))
            {
                i++;
            }

            var tailAfterWs = i < n ? input[i..] : string.Empty;
            return (string.Empty, tailAfterWs);
        }

        string head;
        if (s[i] == '"')
        {
            // Quoted program name: everything up to the next unescaped quote (CreateProcess rule: slashes don't escape here)
            i++;
            var start = i;
            while (i < n && s[i] != '"')
            {
                i++;
            }

            head = input.Substring(start, i - start);
            if (i < n && s[i] == '"')
            {
                i++; // consume closing quote
            }
        }
        else
        {
            // Unquoted program name: read to next whitespace
            var start = i;
            while (i < n && !char.IsWhiteSpace(s[i]))
            {
                i++;
            }

            head = input.Substring(start, i - start);
        }

        // Skip inter-argument whitespace; tail begins at the next non-ws char (or is empty)
        while (i < n && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        var tail = i < n ? input[i..] : string.Empty;

        return (head, tail);
    }

    /// <summary>
    /// Returns the longest possible head (may include spaces) and the tail that starts at the
    /// first *quoted argument*.
    ///
    /// Definition of "quoted argument start":
    /// - A token boundary (start-of-line or preceded by whitespace),
    /// - followed by zero or more backslashes,
    /// - followed by a double-quote ("),
    /// - where the number of immediately preceding backslashes is EVEN (so the quote toggles quoting).
    ///
    /// Notes:
    /// - Quotes appearing mid-token (e.g., C:\Some\"Path\file.txt) do NOT stop the head.
    /// - Trailing spaces before the quoted arg are not included in Head; Tail begins at that quote.
    /// - Leading whitespace before the first token is ignored (Head starts from first non-ws).
    /// Examples:
    ///   C:\app exe -p "1" -q        -> Head: "C:\app exe -p", Tail: "\"1\" -q"
    ///   "\\server\share\" with args  -> Head: "", Tail: "\"\\\\server\\share\\\" with args"
    ///   C:\Some\"Path\file.txt       -> Head: "C:\\Some\\\"Path\\file.txt", Tail: ""
    /// </summary>
    public static (string Head, string Tail) SplitLongestHeadBeforeQuotedArg(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var s = input.AsSpan();
        var n = s.Length;

        // Start at first non-whitespace (we don't treat leading ws as part of Head here)
        var start = 0;
        while (start < n && char.IsWhiteSpace(s[start]))
        {
            start++;
        }

        if (start >= n)
        {
            return (string.Empty, string.Empty);
        }

        // Scan for a quote that OPENS a quoted argument at a token boundary.
        for (var i = start; i < n; i++)
        {
            if (s[i] != '"')
            {
                continue;
            }

            // Count immediate backslashes before this quote
            int j = i - 1, backslashes = 0;
            while (j >= start && s[j] == '\\')
            {
                backslashes++;
                j--;
            }

            // The quote is at a token boundary if the char before the backslashes is start-of-line or whitespace.
            var atTokenBoundary = j < start || char.IsWhiteSpace(s[j]);

            // Even number of backslashes -> this quote toggles quoting (opens if at boundary).
            if (atTokenBoundary && (backslashes % 2 == 0))
            {
                // Trim trailing spaces off Head so Tail starts exactly at the opening quote
                var headEnd = i;
                while (headEnd > start && char.IsWhiteSpace(s[headEnd - 1]))
                {
                    headEnd--;
                }

                var head = input[start..headEnd];
                var tail = input[headEnd..]; // starts at the opening quote
                return (head, tail.Trim());
            }
        }

        // No quoted-arg start found: entire remainder (trimmed right) is the Head
        var wholeHead = input[start..].TrimEnd();
        return (wholeHead, string.Empty);
    }

    /// <summary>
    /// Attempts to expand the path to full physical path, expanding environment variables and shell: monikers.
    /// </summary>
    internal static bool ExpandPathToPhysicalFile(string input, bool expandShell, out string full)
    {
        if (string.IsNullOrEmpty(input))
        {
            full = string.Empty;
            return false;
        }

        var expanded = Environment.ExpandEnvironmentVariables(input);

        var firstSegment = GetFirstPathSegment(expanded);
        if (expandShell && HasShellPrefix(firstSegment) && TryExpandShellMoniker(expanded, out var shellExpanded))
        {
            expanded = shellExpanded;
        }
        else if (firstSegment is "~" or "." or "..")
        {
            expanded = ExpandUserRelative(firstSegment, expanded);
        }

        if (Path.Exists(expanded))
        {
            full = Path.GetFullPath(expanded);
            return true;
        }

        full = expanded; // return the attempted expansion even if it doesn't exist
        return false;
    }

    private static bool TryExpandShellMoniker(string input, out string expanded)
    {
        var separatorIndex = input.IndexOfAny(PathSeparators);
        var shellFolder = separatorIndex > 0 ? input[..separatorIndex] : input;
        var relativePath = separatorIndex > 0 ? input[(separatorIndex + 1)..] : string.Empty;

        if (ShellNames.TryGetFileSystemPath(shellFolder, out var fsPath))
        {
            expanded = Path.GetFullPath(Path.Combine(fsPath, relativePath));
            return true;
        }

        expanded = input;
        return false;
    }

    private static string ExpandUserRelative(string firstSegment, string input)
    {
        // Treat relative paths as relative to the user home directory.
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (firstSegment == "~")
        {
            // Remove "~" (+ optional following separator) before combining.
            var skip = 1;
            if (input.Length > 1 && IsSeparator(input[1]))
            {
                skip++;
            }

            input = input[skip..];
        }

        return Path.GetFullPath(Path.Combine(homeDirectory, input));
    }

    private static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    private static string GetFirstPathSegment(string input)
    {
        var separatorIndex = input.IndexOfAny(PathSeparators);
        return separatorIndex > 0 ? input[..separatorIndex] : input;
    }

    internal static bool HasShellPrefix(string input)
    {
        return input.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || input.StartsWith("::", StringComparison.Ordinal);
    }
}
