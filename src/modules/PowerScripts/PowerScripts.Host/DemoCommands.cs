// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;

namespace PowerScripts.Host;

/// <summary>A fixed, side-effect-free CLI for inspecting the argument binding pipeline.</summary>
internal static class DemoCommands
{
    /// <summary>Echoes declared arguments as JSON without loading configuration or executing another process.</summary>
    public static int Run(IReadOnlyList<string> arguments)
    {
        if (arguments is null || arguments.Count == 0 ||
            arguments.Any(argument => argument is null || argument.Contains('\0')) ||
            !string.Equals(arguments[0], "echo", StringComparison.Ordinal))
        {
            return Fail("Expected 'demo echo --target <value>'; arguments cannot contain NUL characters.");
        }

        string? target = null;
        string mode = "fast";
        int count = 1;
        bool verbose = false;
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Count; index++)
        {
            string option = arguments[index];
            if (option is not ("--target" or "--mode" or "--count" or "--verbose"))
            {
                return Fail($"Unknown demo option '{option}'.");
            }

            if (!supplied.Add(option))
            {
                return Fail($"Demo option '{option}' was supplied more than once.");
            }

            if (option == "--verbose")
            {
                verbose = true;
                continue;
            }

            if (++index >= arguments.Count)
            {
                return Fail($"Missing value for '{option}'.");
            }

            // Every valued option consumes exactly one token, including a target starting with '--'.
            string value = arguments[index];
            switch (option)
            {
                case "--target":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return Fail("The demo target must not be empty.");
                    }

                    target = value;
                    break;
                case "--mode":
                    if (value is not ("fast" or "full"))
                    {
                        return Fail("The demo mode must be 'fast' or 'full'.");
                    }

                    mode = value;
                    break;
                case "--count":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 1 or > 100)
                    {
                        return Fail("The demo count must be an integer between 1 and 100.");
                    }

                    break;
            }
        }

        if (target is null)
        {
            return Fail("The demo target is required.");
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            arguments = arguments.Skip(1).ToArray(),
            parameters = new { target, mode, count, verbose },
        }));
        return 0;
    }

    private static int Fail(string error)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { error }));
        return 1;
    }
}
