// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Helpers;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly bool enableStartMenuSource;
    private readonly bool enableDesktopSource;
    private readonly bool enableRegistrySource;
    private readonly bool enablePathEnvironmentVariableSource;
    private readonly bool includeNonAppsOnDesktop;
    private readonly bool includeNonAppsInStartMenu;
    private readonly List<string> programSuffixes;
    private readonly List<string> runCommandSuffixes;

    public Settings(
        bool enableStartMenuSource = true,
        bool enableDesktopSource = true,
        bool enableRegistrySource = true,
        bool enablePathEnvironmentVariableSource = true,
        bool includeNonAppsOnDesktop = false,
        bool includeNonAppsInStartMenu = true,
        List<string> programSuffixes = null,
        List<string> runCommandSuffixes = null)
    {
        this.enableStartMenuSource = enableStartMenuSource;
        this.enableDesktopSource = enableDesktopSource;
        this.enableRegistrySource = enableRegistrySource;
        this.enablePathEnvironmentVariableSource = enablePathEnvironmentVariableSource;
        this.includeNonAppsOnDesktop = includeNonAppsOnDesktop;
        this.includeNonAppsInStartMenu = includeNonAppsInStartMenu;
        this.programSuffixes = programSuffixes ?? new List<string> { "bat", "appref-ms", "exe", "lnk", "url" };
        this.runCommandSuffixes = runCommandSuffixes ?? new List<string> { "bat", "appref-ms", "exe", "lnk", "url", "cpl", "msc" };
    }

    public bool EnableStartMenuSource => enableStartMenuSource;

    public bool EnableDesktopSource => enableDesktopSource;

    public bool EnableRegistrySource => enableRegistrySource;

    public bool EnablePathEnvironmentVariableSource => enablePathEnvironmentVariableSource;

    public bool IncludeNonAppsOnDesktop => includeNonAppsOnDesktop;

    public bool IncludeNonAppsInStartMenu => includeNonAppsInStartMenu;

    public List<string> ProgramSuffixes => programSuffixes;

    public List<string> RunCommandSuffixes => runCommandSuffixes;

    public static Settings CreateDefaultSettings() => new Settings();

    public static Settings CreateDisabledSourcesSettings() => new Settings(
        enableStartMenuSource: false,
        enableDesktopSource: false,
        enableRegistrySource: false,
        enablePathEnvironmentVariableSource: false);

    public static Settings CreateCustomSuffixesSettings() => new Settings(
        programSuffixes: new List<string> { "exe", "bat" },
        runCommandSuffixes: new List<string> { "exe", "bat", "cmd" });
}
