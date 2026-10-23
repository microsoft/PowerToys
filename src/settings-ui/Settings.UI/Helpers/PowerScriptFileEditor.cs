// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static class PowerScriptFileEditor
{
    public static ProcessStartInfo CreateStartInfo(string path, bool isMetadata)
    {
        var fullPath = Path.GetFullPath(path);
        if (isMetadata && !fullPath.EndsWith(".tool.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Metadata editing requires a .tool.json file.", nameof(path));
        }

        // Use the editor association for both files; a script's Open action can execute it.
        return new(fullPath) { UseShellExecute = true, Verb = "edit" };
    }

    /// <returns>True when Notepad was used because Windows has no appropriate file association.</returns>
    public static bool Open(string path, bool isMetadata)
    {
        var startInfo = CreateStartInfo(path, isMetadata);
        if (!File.Exists(startInfo.FileName))
        {
            throw new FileNotFoundException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_EditorFileMissing"), startInfo.FileName);
        }

        try
        {
            using var editor = Process.Start(startInfo);
            return false;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 1155 or 31)
        {
            Logger.LogWarning($"No Windows edit association for '{startInfo.FileName}': {ex.Message}. Opening in Notepad.");
            var fallback = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"))
            {
                UseShellExecute = false,
            };
            fallback.ArgumentList.Add(startInfo.FileName);
            using var editor = Process.Start(fallback)
                ?? throw new InvalidOperationException(ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_EditorLaunchFailed"));
            return true;
        }
    }
}
