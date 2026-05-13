// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
        Guid? guid = null;
        if (guidElement.ValueKind == JsonValueKind.String)
        {
            var guidString = guidElement.GetString();
            if (!string.IsNullOrWhiteSpace(guidString) && Guid.TryParse(guidString, out var parsedGuid))
            {
                guid = parsedGuid;
            }
        }

        profileElement.TryGetProperty("icon", out var iconElement);
        var icon = iconElement.ValueKind == JsonValueKind.String ? iconElement.GetString() : null;

        return new TerminalProfile(terminal, name, guid, hidden, icon);
    }

    /// <summary>
    /// Resolve a profile icon to a usable path. For built-in profiles without
    /// an explicit icon, looks up the GUID-based icon in the Terminal package's
    /// ProfileIcons folder. Handles ms-appx:/// URIs by mapping them to the
    /// package install directory. Passes through file paths and font glyphs
    /// as-is. Falls back to the terminal package logo as a last resort.
    /// </summary>
    public static string ResolveProfileIcon(TerminalProfile profile)
    {
        var icon = profile.Icon;

        if (string.IsNullOrEmpty(icon))
        {
            // Built-in profiles don't have an icon in settings.json.
            // Their icons are stored by GUID in the Terminal package.
            if (profile.Identifier.HasValue)
            {
                var guidIcon = TryResolveGuidIcon(profile.Terminal.InstallPath, profile.Identifier.Value);
                if (guidIcon is not null)
                {
                    return guidIcon;
                }
            }

            return profile.Terminal.LogoPath;
        }

        if (icon.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveAppxPath(profile.Terminal.InstallPath, icon) ?? profile.Terminal.LogoPath;
        }

        return icon;
    }

    private static string? TryResolveGuidIcon(string installPath, Guid guid)
    {
        var profileIconsDir = Path.Combine(installPath, "ProfileIcons");
        if (!Directory.Exists(profileIconsDir))
        {
            return null;
        }

        var guidStr = guid.ToString("B");
        foreach (var scale in new[] { ".scale-200", ".scale-150", ".scale-100" })
        {
            var path = Path.Combine(profileIconsDir, $"{guidStr}{scale}.png");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? ResolveAppxPath(string installPath, string msAppxUri)
    {
        var relativePath = msAppxUri.Substring("ms-appx:///".Length).Replace('/', '\\');
        var resolved = Path.Combine(installPath, relativePath);
        if (File.Exists(resolved))
        {
            return resolved;
        }

        var dir = Path.GetDirectoryName(resolved);
        var name = Path.GetFileNameWithoutExtension(resolved);
        var ext = Path.GetExtension(resolved);

        foreach (var scale in new[] { ".scale-200", ".scale-150", ".scale-100" })
        {
            var scaled = Path.Combine(dir!, $"{name}{scale}{ext}");
            if (File.Exists(scaled))
            {
                return scaled;
            }
        }

        return null;
    }
}
