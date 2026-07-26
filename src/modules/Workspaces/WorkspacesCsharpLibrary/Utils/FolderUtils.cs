// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Library;

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

    // Note: the same path should be used in SnapshotTool and Launcher
    public static string DataFolder()
    {
        return Path.GetDirectoryName(SettingsUtils.Default.GetSettingsFilePath("Workspaces", "workspaces.json"))!;
    }
}
