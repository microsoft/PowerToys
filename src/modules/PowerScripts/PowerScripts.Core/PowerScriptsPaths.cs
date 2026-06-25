// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core;

/// <summary>
/// Well-known filesystem locations for the PowerScripts module. The scripts root can be overridden
/// (env var or explicit path) which keeps tests and ad-hoc runs hermetic.
/// </summary>
public static class PowerScriptsPaths
{
    /// <summary>Environment variable that overrides the default scripts root.</summary>
    public const string RootEnvironmentVariable = "POWERSCRIPTS_ROOT";

    /// <summary>The folder a single script lives in must contain a file with this name.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>
    /// Default scripts root:
    /// <c>%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts</c>.
    /// </summary>
    public static string DefaultScriptsRoot
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Microsoft", "PowerToys", "PowerScripts", "scripts");
        }
    }

    /// <summary>
    /// Resolves the scripts root, honoring the environment override, then the default.
    /// </summary>
    public static string ResolveScriptsRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return explicitRoot;
        }

        var fromEnv = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        return string.IsNullOrWhiteSpace(fromEnv) ? DefaultScriptsRoot : fromEnv;
    }
}
