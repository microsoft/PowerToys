// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly bool globalIfURI;
    private readonly string showHistory;

    public Settings(
        bool globalIfURI = false,
        string showHistory = "None")
    {
        this.globalIfURI = globalIfURI;
        this.showHistory = showHistory;
    }

    public bool GlobalIfURI => globalIfURI;

    public string ShowHistory => showHistory;

    public static Settings CreateDefaultSettings() => new Settings();

    public static Settings CreateGlobalUriSettings() => new Settings(globalIfURI: true);

    public static Settings CreateHistoryEnabledSettings() => new Settings(showHistory: "5");

    public static Settings CreateHistoryDisabledSettings() => new Settings(showHistory: "None");
}
