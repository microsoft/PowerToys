// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

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

    // User-writable working folder for the Editor's transient files (icons,
    // temp-project handoff) AND the legacy / no-service fallback store.
    //
    // v6 note: the *protected* settings store does NOT live here — it is the
    // service-managed blob under %ProgramData% (see SettingsPaths).  The
    // Editor reads/writes the real settings through PTSettingsClient
    // (GetBlob / PutBlob); this %LocalAppData% path is only the working dir and
    // the no-service fallback, both of which must stay user-writable.
    public static string DataFolder()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\PowerToys\\Workspaces";
    }

    // The pre-v6 location.  Same as DataFolder() now; kept as a distinct name
    // for the one-shot migration source and the no-service fallback.
    public static string LegacyDataFolder()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\PowerToys\\Workspaces";
    }
}
