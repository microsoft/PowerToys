// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace WorkspacesEditor.Utils
{
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
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\PowerToys\\Workspaces";
        }
    }
}
