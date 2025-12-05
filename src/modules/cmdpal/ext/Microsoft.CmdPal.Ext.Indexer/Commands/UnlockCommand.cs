// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

public partial class UnlockCommand : InvokableCommand
{
    internal static IconInfo UnlockIcon { get; } = new("\uE785");

    private readonly string _full_path;
    private const string PowerToyName = "File Locksmith";
    private const string LastRunFileName = "last-run.log";
    private const string FileLocksmithUIExe = "PowerToys.FileLocksmithUI.exe";

    private static bool UnlockWithFileLockSmith(string fullPath)
    {
        try
        {
            // 1. Write file path to the temp file
            if (!WritePathToIpcFile(fullPath))
            {
                return false;
            }

            // 2. Launch FileLocksmith UI
            return LaunchFileLocksmithUI();
        }
        catch (Exception ex)
        {
            // Log error if needed
            Logger.LogError($"Error launching FileLocksmith: {ex.Message}");
            return false;
        }
    }

    private static bool WritePathToIpcFile(string filePath)
    {
        try
        {
            var ipcFilePath = GetIpcFilePath();

            // Ensure directory exists
            var directory = Path.GetDirectoryName(ipcFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write path in UTF-16 format (same as C++ FileLocksmith)
            using var fileStream = new FileStream(ipcFilePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fileStream, Encoding.Unicode);

            // Write the file path
            writer.Write(filePath.ToCharArray());

            // Write newline character (UTF-16)
            writer.Write('\n');

            // Write empty line as end marker
            writer.Write('\n');

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            return false;
        }
    }

    private static bool LaunchFileLocksmithUI()
    {
        try
        {
            var uiExePath = GetFileLocksmithUIPath();

            if (!File.Exists(uiExePath))
            {
                System.Diagnostics.Debug.WriteLine($"FileLocksmith UI not found: {uiExePath}");
                return false;
            }

            // Launch the UI process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = uiExePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
            };

            Process.Start(processStartInfo);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to launch FileLocksmith UI: {ex.Message}");
            return false;
        }
    }

    private static string GetIpcFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", PowerToyName, LastRunFileName);
    }

    private static string GetFileLocksmithUIPath()
    {
        // First try to use PowerToysPathResolver to get the installation directory
        var powerToysInstallPath = PowerToysPathResolver.GetPowerToysInstallPath();
        if (!string.IsNullOrEmpty(powerToysInstallPath))
        {
            var uiPath = Path.Combine(powerToysInstallPath, "WinUI3Apps", FileLocksmithUIExe);
            if (File.Exists(uiPath))
            {
                return uiPath;
            }
        }

        return string.Empty;
    }

    public UnlockCommand(string fullPath)
    {
        _full_path = fullPath;

        // Set command properties
        Name = "Unlock with Powertoys.FileLocksmith";
        Icon = UnlockIcon;
    }

    public override CommandResult Invoke()
    {
        try
        {
            // Simple log if path is empty
            if (string.IsNullOrEmpty(_full_path))
            {
                Logger.LogError("Empty file path");
                return CommandResult.GoHome();
            }

            // Launch FileLocksmith
            var success = UnlockWithFileLockSmith(_full_path);

            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Error: {ex.Message}");
        }
    }
}
