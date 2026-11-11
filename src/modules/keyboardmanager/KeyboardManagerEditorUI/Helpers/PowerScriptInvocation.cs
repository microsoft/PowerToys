// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Encodes ordinary PowerScript hotkey invocations without a shell or a separate binding store.</summary>
    public static class PowerScriptInvocation
    {
        private const int MaximumCommandLineLength = 32767;
        private const string ConsumerId = "keyboard-manager";
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static string BuildArguments(string scriptId, IReadOnlyDictionary<string, string?> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (!IsScriptId(scriptId))
            {
                throw new ArgumentException("The PowerScript ID is invalid.", nameof(scriptId));
            }

            var arguments = new StringBuilder("run ").Append(scriptId)
                .Append(" --consumer ").Append(ConsumerId).Append(" --no-consent");
            foreach (var pair in parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Contains('\0') || pair.Value?.Contains('\0') == true)
                {
                    throw new ArgumentException("PowerScript parameter names and values cannot contain null characters.", nameof(parameters));
                }

                if (pair.Value is null)
                {
                    continue;
                }

                arguments.Append(" --set-base64 ")
                    .Append(Convert.ToBase64String(StrictUtf8.GetBytes(pair.Key + "\0" + pair.Value)));
            }

            return arguments.ToString();
        }

        /// <summary>Recognizes only silent script invocations that the editor can round-trip without losing arguments.</summary>
        public static bool TryParseArguments(string? arguments, out string scriptId, out IReadOnlyDictionary<string, string?> parameters)
        {
            scriptId = string.Empty;
            parameters = new Dictionary<string, string?>();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return false;
            }

            var tokens = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3 || tokens[0] != "run" || !IsScriptId(tokens[1]))
            {
                return false;
            }

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            bool noConsent = false;
            bool hasConsumer = false;
            try
            {
                for (int index = 2; index < tokens.Length; index++)
                {
                    switch (tokens[index])
                    {
                        case "--no-consent" when !noConsent:
                            noConsent = true;
                            break;
                        case "--consumer" when !hasConsumer && index + 1 < tokens.Length && tokens[index + 1] == ConsumerId:
                            hasConsumer = true;
                            index++;
                            break;
                        case "--set-base64":
                            int count = 0;
                            while (index + 1 < tokens.Length && !tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                            {
                                string decoded = StrictUtf8.GetString(Convert.FromBase64String(tokens[++index]));
                                int separator = decoded.IndexOf('\0');
                                if (separator <= 0 || decoded.IndexOf('\0', separator + 1) >= 0 ||
                                    !values.TryAdd(decoded[..separator], decoded[(separator + 1)..]))
                                {
                                    return false;
                                }

                                count++;
                            }

                            if (count == 0)
                            {
                                return false;
                            }

                            break;
                        default:
                            return false;
                    }
                }
            }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
            {
                return false;
            }

            if (!noConsent)
            {
                return false;
            }

            // Older prototype bindings did not include attribution. Saving them adds the current consumer.
            scriptId = tokens[1];
            parameters = values;
            return true;
        }

        public static bool TryValidateLength(string hostPath, string arguments, out string? error)
        {
            if (string.IsNullOrWhiteSpace(hostPath) || hostPath.IndexOfAny(['"', '\0', '\r', '\n']) >= 0)
            {
                error = "The PowerScripts Host path is invalid.";
                return false;
            }

            // CreateProcess counts UTF-16 characters, executable quotes, a separating space, and the final NUL.
            if ((long)hostPath.Length + 2 + 1 + arguments.Length + 1 > MaximumCommandLineLength)
            {
                error = "The script parameters exceed the Windows command-line limit.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsScriptId(string? id) =>
            !string.IsNullOrEmpty(id) && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
