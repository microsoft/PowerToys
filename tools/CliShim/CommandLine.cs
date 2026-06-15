// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.CliShim;

/// <summary>
/// Helpers for working with the raw process command line. Kept in its own internal type
/// (rather than inside <see cref="Program"/>) so the parsing logic can be unit tested by
/// linking this single source file into the test project.
/// </summary>
internal static class CommandLine
{
    /// <summary>
    /// Returns the command line with its first token (argv[0]) removed, following the C
    /// runtime rule for the program name: leading whitespace is skipped first; then, when
    /// argv[0] starts with a quote it ends at the next quote (no backslash-escaping for the
    /// program name), otherwise it ends at the first whitespace. Whitespace before the first
    /// real argument is then trimmed.
    /// </summary>
    /// <param name="commandLine">The raw process command line (for example <see cref="System.Environment.CommandLine"/>).</param>
    /// <returns>The remaining arguments, verbatim.</returns>
    internal static string StripArgumentZero(string commandLine)
    {
        int index = 0;

        // Skip leading whitespace before argv[0]. The OS loader never produces this, but a
        // non-shell parent that calls CreateProcessW with a padded lpCommandLine can, and
        // without this the unquoted scan below would stall at index 0 and leak the program
        // name into the forwarded arguments.
        while (index < commandLine.Length && (commandLine[index] == ' ' || commandLine[index] == '\t'))
        {
            index++;
        }

        if (index < commandLine.Length && commandLine[index] == '"')
        {
            index++;
            while (index < commandLine.Length && commandLine[index] != '"')
            {
                index++;
            }

            if (index < commandLine.Length)
            {
                index++; // Consume the closing quote.
            }
        }
        else
        {
            while (index < commandLine.Length && commandLine[index] != ' ' && commandLine[index] != '\t')
            {
                index++;
            }
        }

        while (index < commandLine.Length && (commandLine[index] == ' ' || commandLine[index] == '\t'))
        {
            index++;
        }

        return commandLine[index..];
    }
}
