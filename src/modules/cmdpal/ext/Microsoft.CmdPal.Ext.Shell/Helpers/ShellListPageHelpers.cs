// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Pages;
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

    internal static ListItem? ListItemForCommandString(string query, Action<string>? addToHistory, ITelemetryService? telemetryService)
    {
        var li = new ListItem();

        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        ShellHelpers.ParseExecutableAndArgs(searchText, out var exe, out var args);

        var exeExists = false;
        var pathIsDir = false;
        var fullExePath = string.Empty;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            // Use Task.Run with timeout - this will actually timeout even if the sync operations don't respond to cancellation
            var pathResolutionTask = Task.Run(
                () =>
            {
                // Don't check cancellation token here - let the Task timeout handle it
                exeExists = ShellListPageHelpers.FileExistInPath(exe, out fullExePath);
                pathIsDir = Directory.Exists(expanded);
            },
                CancellationToken.None); // Use None here since we're handling timeout differently

            // Wait for either completion or timeout
            pathResolutionTask.Wait(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        if (exeExists)
        {
            // TODO we need to probably get rid of the settings for this provider entirely
            var exeItem = ShellListPage.CreateExeItem(exe, args, fullExePath, addToHistory, telemetryService);
            li.Command = exeItem.Command;
            li.Title = exeItem.Title;
            li.Subtitle = exeItem.Subtitle;
            li.Icon = exeItem.Icon;
            li.MoreCommands = exeItem.MoreCommands;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query, addToHistory, telemetryService);
            li.Command = pathItem.Command;
            li.Title = pathItem.Title;
            li.Subtitle = pathItem.Subtitle;
            li.Icon = pathItem.Icon;
            li.MoreCommands = pathItem.MoreCommands;
        }
        else if (System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            li.Command = new OpenUrlWithHistoryCommand(searchText, addToHistory, telemetryService) { Result = CommandResult.Dismiss() };
            li.Title = searchText;
        }
        else
        {
            return null;
        }

        if (li is not null)
        {
            li.TextToSuggest = searchText;
        }

        return li;
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
