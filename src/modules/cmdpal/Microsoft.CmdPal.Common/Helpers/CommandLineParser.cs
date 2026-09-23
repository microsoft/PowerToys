// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Common.Helpers;

public static class CommandLineParser
{
    /// <summary>
    /// Parses a full command line, including the executable name, using CommandLineToArgvW.
    /// The first token uses executable-name quoting rules and is included in the result.
    /// </summary>
    public static string[] Parse(string commandLine)
    {
        unsafe
        {
            var argv = PInvoke.CommandLineToArgv(commandLine, out var argc);

            if (argv == null || argc == 0)
            {
                return Array.Empty<string>();
            }

            try
            {
                var args = new string[argc];

                for (var i = 0; i < argc; i++)
                {
                    args[i] = new string(argv[i]);
                }

                return args;
            }
            finally
            {
                PInvoke.LocalFree(new HLOCAL(argv));
            }
        }
    }

    /// <summary>
    /// Parses arguments without an executable name, using argument quoting rules for every token.
    /// </summary>
    public static string[] ParseArguments(string arguments)
    {
        // CommandLineToArgvW treats argv[0] specially, so supply and discard a dummy executable.
        return Parse("cmdpal.exe " + arguments).Skip(1).ToArray();
    }
}
