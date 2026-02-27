// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal sealed class ProfilePathAndUsernameRuleProvider : ISanitizationRuleProvider
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(SanitizerDefaults.DefaultMatchTimeoutMs);

    private readonly Dictionary<string, string> _profilePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usernames = new(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> CommonPathParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Users", "home", "Documents", "Desktop", "AppData", "Local", "Roaming",
        "Pictures", "Videos", "Music", "Downloads", "Program Files", "Windows",
        "System32", "bin", "usr", "var", "etc", "opt", "tmp",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> CommonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "user", "test", "guest", "public", "system", "service",
        "default", "temp", "local", "shared", "common", "data", "config",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public ProfilePathAndUsernameRuleProvider()
    {
        DetectSystemPaths();
    }

    public IEnumerable<SanitizationRule> GetRules()
    {
        List<SanitizationRule> rules = [];

        // Profile path rules (ordered longest-first)
        var orderedRules = _profilePaths
            .Where(p => !string.IsNullOrEmpty(p.Key))
            .OrderByDescending(p => p.Key.Length);

        foreach (var profilePath in orderedRules)
        {
            try
            {
                var normalizedPath = profilePath.Key
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var escapedPath = Regex.Escape(normalizedPath);

                var pattern = escapedPath + @"(?:[/\\]*)";
                var rx = new Regex(pattern, SanitizerDefaults.DefaultOptions, DefaultTimeout);

                rules.Add(new(rx, profilePath.Value, $"Profile path: {profilePath}"));
            }
            catch
            {
                // Skip problematic paths
            }
        }

        // Username rules
        foreach (var username in _usernames.Where(u => !string.IsNullOrEmpty(u) && u.Length > 2))
        {
            try
            {
                if (!IsLikelyUsername(username))
                {
                    continue;
                }

                var rx = new Regex(@"\b" + Regex.Escape(username) + @"\b", SanitizerDefaults.DefaultOptions, DefaultTimeout);
                rules.Add(new(rx, "[USERNAME_REDACTED]", $"Username: {username}"));
            }
            catch
            {
                // Skip problematic usernames
            }
        }

        return rules;
    }

    public IReadOnlyDictionary<string, string> GetDetectedProfilePaths() => _profilePaths;

    public IReadOnlyCollection<string> GetDetectedUsernames() => _usernames;

    private void DetectSystemPaths()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
            {
                _profilePaths.Add(userProfile, "[USER_PROFILE_DIR]");
                var username = Path.GetFileName(userProfile);
                if (!string.IsNullOrEmpty(username) && username.Length > 2)
                {
                    _usernames.Add(username);
                }
            }

            Environment.SpecialFolder[] profileFolders =
            [
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.StartMenu,
                Environment.SpecialFolder.Startup,
                Environment.SpecialFolder.DesktopDirectory
            ];

            foreach (var folder in profileFolders)
            {
                var dir = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                var added = _profilePaths.TryAdd(dir, $"[{folder.ToString().ToUpperInvariant()}_DIR]");
                if (!added)
                {
                    continue;
                }
            }

            string[] envVars = ["USERPROFILE", "HOME", "OneDrive", "OneDriveCommercial"];
            foreach (var envVar in envVars)
            {
                var envPath = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
                {
                    _profilePaths.TryAdd(envPath, $"[{envVar.ToUpperInvariant()}_DIR]");
                }
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Error detecting system profile paths and usernames", ex);
        }
    }

    private static bool IsLikelyUsername(string username) =>
        !CommonWords.Contains(username) &&
        username.Length is >= 3 and <= 50 &&
        !username.All(char.IsDigit);
}
