// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Helpers
{
    public static class TerminalHelper
    {
        /// <summary>
        /// Return the arguments for launch Windows Terminal
        /// </summary>
        /// <param name="profileName">Name of the Terminal profile</param>
        /// <param name="openNewTab">Whether to launch the profile in a new tab</param>
        public static string GetArguments(string profileName, bool openNewTab)
        {
            return openNewTab ? $"--window 0 nt --profile \"{profileName}\"" : $"--profile \"{profileName}\"";
        }

        /// <summary>
        /// Return a list of profiles for the Windows Terminal
        /// </summary>
        /// <param name="terminal">Windows Terminal package</param>
        /// <param name="settingsJson">Content of the settings JSON file of the Terminal</param>
        public static List<TerminalProfile> ParseSettings(TerminalPackage terminal, string settingsJson)
        {
            var profiles = new List<TerminalProfile>();

            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
            };

            var json = JsonDocument.Parse(settingsJson, options);

            json.RootElement.TryGetProperty("profiles", out JsonElement profilesElement);
            if (profilesElement.ValueKind != JsonValueKind.Object)
            {
                return profiles;
            }

            profilesElement.TryGetProperty("list", out JsonElement profilesList);
            if (profilesList.ValueKind != JsonValueKind.Array)
            {
                return profiles;
            }

            foreach (var profile in profilesList.EnumerateArray())
            {
                profiles.Add(ParseProfile(terminal, profile));
            }

            return profiles;
        }

        /// <summary>
        /// Return a profile for the Windows Terminal
        /// </summary>
        /// <param name="terminal">Windows Terminal package</param>
        /// <param name="profileElement">Profile from the settings JSON file</param>
        public static TerminalProfile ParseProfile(TerminalPackage terminal, JsonElement profileElement)
        {
            profileElement.TryGetProperty("name", out JsonElement nameElement);
            var name = nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString() : null;

            profileElement.TryGetProperty("hidden", out JsonElement hiddenElement);
            var hidden = (hiddenElement.ValueKind == JsonValueKind.False || hiddenElement.ValueKind == JsonValueKind.True) && hiddenElement.GetBoolean();

            profileElement.TryGetProperty("guid", out JsonElement guidElement);
            var guid = guidElement.ValueKind == JsonValueKind.String ? Guid.Parse(guidElement.GetString()) : null as Guid?;

            return new TerminalProfile(terminal, name, guid, hidden);
        }
    }
}
