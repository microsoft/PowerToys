// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Workspaces.TestApp
{
    internal sealed class LaunchOptions
    {
        internal string Title { get; private set; } = "Workspaces UI test app";

        internal string Payload { get; private set; } = string.Empty;

        internal string? WaitEventName { get; private set; }

        internal string? StartedEventName { get; private set; }

        internal string? ReadyEventName { get; private set; }

        internal bool Minimized { get; private set; }

        internal static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions();
            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--title":
                        options.Title = ReadValue(args, ref index);
                        break;
                    case "--payload":
                        options.Payload = ReadValue(args, ref index);
                        break;
                    case "--wait-event":
                        options.WaitEventName = ReadEventName(args, ref index);
                        break;
                    case "--started-event":
                        options.StartedEventName = ReadEventName(args, ref index);
                        break;
                    case "--ready-event":
                        options.ReadyEventName = ReadEventName(args, ref index);
                        break;
                    case "--minimized":
                        options.Minimized = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{args[index]}'.");
                }
            }

            return options;
        }

        private static string ReadValue(string[] args, ref int index)
        {
            string option = args[index];
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for '{option}'.");
            }

            return args[++index];
        }

        private static string ReadEventName(string[] args, ref int index)
        {
            string option = args[index];
            string name = ReadValue(args, ref index);
            string localName = name.StartsWith(@"Local\", StringComparison.Ordinal) ? name[6..] : name;
            if (string.IsNullOrWhiteSpace(localName) || localName.Contains('\\'))
            {
                throw new ArgumentException($"'{option}' requires a local event name, optionally prefixed with 'Local\\'.");
            }

            return name;
        }
    }
}
