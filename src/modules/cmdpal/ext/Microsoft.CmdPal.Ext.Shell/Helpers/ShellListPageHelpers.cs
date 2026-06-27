// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Text;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class ShellListPageHelpers
{
    internal static bool TryGetEnvironmentVariableCompletionPrefix(string input, out int tokenStart, out string prefix)
    {
        tokenStart = -1;
        prefix = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var openTokenStart = -1;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '%')
            {
                continue;
            }

            if (openTokenStart == -1)
            {
                openTokenStart = i;
            }
            else
            {
                openTokenStart = -1;
            }
        }

        if (openTokenStart == -1 || openTokenStart == input.Length - 1)
        {
            return false;
        }

        tokenStart = openTokenStart;
        prefix = input.Substring(openTokenStart + 1);
        return true;
    }

    internal static string CompleteEnvironmentVariableToken(string input, string variableName)
    {
        if (string.IsNullOrEmpty(variableName) ||
            !TryGetEnvironmentVariableCompletionPrefix(input, out var tokenStart, out var _))
        {
            return input;
        }

        return string.Concat(input.AsSpan(0, tokenStart), "%", variableName, "%");
    }

    internal static string ExpandEnvironmentVariablesForPath(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var expandedInput = new StringBuilder(input.Length);
        var copiedIndex = 0;
        var foundExpandableToken = false;
        var openTokenStart = -1;

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '%')
            {
                continue;
            }

            if (openTokenStart == -1)
            {
                openTokenStart = i;
                continue;
            }

            var variableName = input.Substring(openTokenStart + 1, i - openTokenStart - 1);
            if (string.IsNullOrEmpty(variableName))
            {
                openTokenStart = -1;
                continue;
            }

            if (!TryFindEnvironmentVariable(variableName, out var canonicalName, out var variableValue) ||
                string.IsNullOrEmpty(variableValue))
            {
                return input;
            }

            expandedInput.Append(input, copiedIndex, openTokenStart - copiedIndex);
            expandedInput.Append('%');
            expandedInput.Append(canonicalName);
            expandedInput.Append('%');
            copiedIndex = i + 1;
            foundExpandableToken = true;
            openTokenStart = -1;
        }

        if (!foundExpandableToken)
        {
            return input;
        }

        expandedInput.Append(input, copiedIndex, input.Length - copiedIndex);
        return Environment.ExpandEnvironmentVariables(expandedInput.ToString());
    }

    private static bool TryFindEnvironmentVariable(string variableName, out string canonicalName, out string? variableValue)
    {
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is string name &&
                name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                canonicalName = name;
                variableValue = variable.Value as string;
                return true;
            }
        }

        canonicalName = string.Empty;
        variableValue = null;
        return false;
    }

    internal static bool FileExistInPath(string filename)
    {
        return FileExistInPath(filename, out var _);
    }

    internal static bool FileExistInPath(string filename, out string fullPath, CancellationToken? token = null)
    {
        return ShellHelpers.FileExistInPath(filename, out fullPath, token ?? CancellationToken.None);
    }

    /// <summary>
    /// This is a version of ParseExecutableAndArgs that handles whitespace in
    /// paths better. It will try to find the first matching executable in the
    /// input string.
    ///
    /// If the input is quoted, it will treat everything inside the quotes as
    /// the executable. If the input is not quoted, it will try to find the
    /// first segment that matches
    /// </summary>
    public static void NormalizeCommandLineAndArgs(string input, out string executable, out string arguments)
    {
        var normalized = CommandLineNormalizer.NormalizeCommandLine(input, allowDirectory: true);
        var segments = normalized.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        executable = string.Empty;
        arguments = string.Empty;
        if (segments.Length == 0)
        {
            return;
        }

        executable = segments[0];
        if (segments.Length > 1)
        {
            arguments = ShellArgumentBuilder.BuildArguments(segments[1..]);
        }
    }
}
