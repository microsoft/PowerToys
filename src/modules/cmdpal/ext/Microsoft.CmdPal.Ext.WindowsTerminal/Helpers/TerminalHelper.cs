// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public static class TerminalHelper
{
    /// <summary>
    /// Return the arguments for launch Windows Terminal
    /// </summary>
    /// <param name="profileName">Name of the Terminal profile</param>
    /// <param name="openNewTab">Whether to launch the profile in a new tab</param>
    /// <param name="openQuake">Whether to launch the profile in the quake window</param>
    public static string GetArguments(string profileName, bool openNewTab, bool openQuake)
    {
        var argsPrefix = string.Empty;
        if (openQuake)
        {
            // It does not matter whether we add the "nt" argument here; when specifying the
            // _quake window explicitly, Windows Terminal will always open a new tab when the
            // window exists, or open a new window when it does not yet.
            argsPrefix = "--window _quake";
        }
        else if (openNewTab)
        {
            argsPrefix = "--window 0 nt";
        }

        return $"{argsPrefix} --profile \"{profileName}\"";
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
            AllowTrailingCommas = true,
        };

        var json = JsonDocument.Parse(settingsJson, options);
        JsonElement profilesList;

        json.RootElement.TryGetProperty("profiles", out var profilesElement);
        if (profilesElement.ValueKind == JsonValueKind.Object)
        {
            profilesElement.TryGetProperty("list", out profilesList);
            if (profilesList.ValueKind != JsonValueKind.Array)
            {
                return profiles;
            }
        }
        else if (profilesElement.ValueKind == JsonValueKind.Array)
        {
            profilesList = profilesElement;
        }
        else
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
        profileElement.TryGetProperty("name", out var nameElement);
        var name = nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString() : null;

        profileElement.TryGetProperty("hidden", out var hiddenElement);
        var hidden = (hiddenElement.ValueKind == JsonValueKind.False || hiddenElement.ValueKind == JsonValueKind.True) && hiddenElement.GetBoolean();

        profileElement.TryGetProperty("guid", out var guidElement);
        var guid = guidElement.ValueKind == JsonValueKind.String ? Guid.Parse(guidElement.GetString()) : null as Guid?;

        profileElement.TryGetProperty("icon", out var iconElement);
        var icon = iconElement.ValueKind == JsonValueKind.String ? iconElement.GetString() : null;

        return new TerminalProfile(terminal, name, guid, hidden, icon);
    }
}
