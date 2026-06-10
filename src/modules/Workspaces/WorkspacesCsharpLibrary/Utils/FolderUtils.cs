// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using WorkspacesCsharpLibrary.SettingsService;

namespace WorkspacesCsharpLibrary.Utils;

public class FolderUtils
{
    public static string Desktop()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    public static string Temp()
    {
        return Path.GetTempPath();
    }

    // v6: settings live in the service-managed per-user folder under
    // %ProgramData% (ACL'd so only PTWorkspacesSvc can write).  Callers
    // that just want to *read* (Launcher, Editor's initial load) can still
    // use this path directly — the user has Read+Execute via the DACL.
    // Writers must round-trip through WorkspacesSvcClient.PutSettings.
    public static string DataFolder()
    {
        return SettingsPaths.CurrentUserFolder();
    }

    // The pre-v6 location.  Exposed only for the one-shot migration; nothing
    // else should be using it.
    public static string LegacyDataFolder()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\PowerToys\\Workspaces";
    }
}
