// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class ShellListPageHelpers
{
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
            arguments = ArgumentBuilder.BuildArguments(segments[1..]);
        }
    }

    private static class ArgumentBuilder
    {
        internal static string BuildArguments(string[] arguments)
        {
            if (arguments.Length <= 0)
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder();
            foreach (var argument in arguments)
            {
                AppendArgument(stringBuilder, argument);
            }

            return stringBuilder.ToString();
        }

        private static void AppendArgument(StringBuilder stringBuilder, string argument)
        {
            if (stringBuilder.Length > 0)
            {
                stringBuilder.Append(' ');
            }

            if (argument.Length == 0 || ShouldBeQuoted(argument))
            {
                stringBuilder.Append('\"');
                var index = 0;
                while (index < argument.Length)
                {
                    var c = argument[index++];
                    if (c == '\\')
                    {
                        var numBackSlash = 1;
                        while (index < argument.Length && argument[index] == '\\')
                        {
                            index++;
                            numBackSlash++;
                        }

                        if (index == argument.Length)
                        {
                            stringBuilder.Append('\\', numBackSlash * 2);
                        }
                        else if (argument[index] == '\"')
                        {
                            stringBuilder.Append('\\', (numBackSlash * 2) + 1);
                            stringBuilder.Append('\"');
                            index++;
                        }
                        else
                        {
                            stringBuilder.Append('\\', numBackSlash);
                        }

                        continue;
                    }

                    if (c == '\"')
                    {
                        stringBuilder.Append('\\');
                        stringBuilder.Append('\"');
                        continue;
                    }

                    stringBuilder.Append(c);
                }

                stringBuilder.Append('\"');
            }
            else
            {
                stringBuilder.Append(argument);
            }
        }

        private static bool ShouldBeQuoted(string s)
        {
            foreach (var c in s)
            {
                if (char.IsWhiteSpace(c) || c == '\"')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
