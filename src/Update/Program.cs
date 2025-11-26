// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Update;

[SupportedOSPlatform("windows")]
internal sealed partial class Program
{
    private static readonly string _installerPath = Path.Combine(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        "Updates"));

    private static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Environment.Exit(1);
            return;
        }

        string action = args[0];

        switch (action)
        {
            case UpdateStage.UPDATENOWLAUNCHSTAGE1:
                await PerformUpdateNowStage1();
                break;
            case UpdateStage.UPDATENOWLAUNCHSTAGE2:
                if (args.Length < 2)
                {
                    Environment.Exit(1);
                }

                await PerformUpdateNowStage2(args[1]);
                break;
            default:
                break;
        }
    }

    private static async Task PerformUpdateNowStage2(string installerPath)
    {
        Process installerProcess = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/passive /norestart",
                UseShellExecute = true,
            },
        };

        installerProcess.Start();
        await installerProcess.WaitForExitAsync();

        if (installerProcess.ExitCode == 0)
        {
            UpdateSettingsHelper.ProcessNoUpdateAvailable();
        }
        else
        {
            UpdateSettingsHelper.SetUpdateState(UpdatingSettings.UpdatingState.ErrorDownloading);
        }
    }

    private static async Task PerformUpdateNowStage1()
    {
        UpdateSettingsHelper.TriggerUpdateCheck();
        UpdateSettingsHelper.UpdateInfo updateInfo = await UpdateSettingsHelper.GetUpdateAvailableInfo();

        if (updateInfo is not UpdateSettingsHelper.UpdateInfo.UpdateAvailable ua)
        {
            // No update found
            Environment.Exit(1);
            return;
        }

        // Copy itsself to the temp folder
        File.Copy("PowerToys.Update.exe", Path.Combine(Path.GetTempPath(), "PowerToys.Update.exe"), true);

        string? installerFilePath = null;

        switch (UpdateSettingsHelper.GetUpdateState())
        {
            case UpdatingSettings.UpdatingState.ReadyToDownload:
            case UpdatingSettings.UpdatingState.ErrorDownloading:
                CleanupUpdates();
                installerFilePath = await DownloadFile(ua.InstallerDownloadUrl.ToString(), ua.InstallerFilename);
                break;
            case UpdatingSettings.UpdatingState.ReadyToInstall:
                installerFilePath = Path.Combine(_installerPath, ua.InstallerFilename);
                if (!File.Exists(installerFilePath))
                {
                    // Installer not found
                    Environment.Exit(1);
                    return;
                }

                break;
            case UpdatingSettings.UpdatingState.UpToDate:
                Environment.Exit(0);
                return;
        }

        if (installerFilePath == null)
        {
            UpdateSettingsHelper.SetUpdateState(UpdatingSettings.UpdatingState.ErrorDownloading);
            Environment.Exit(1);
            return;
        }

        IntPtr runnerHwnd = FindWindowW("pt_tray_icon_window_class");

        if (runnerHwnd != IntPtr.Zero)
        {
            SendMessageW(runnerHwnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // Send WM_CLOSE
        }

        string arguments = $"{UpdateStage.UPDATENOWLAUNCHSTAGE2} \"{installerFilePath}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Path.GetTempPath(), "PowerToys.Update.exe"),
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
        });
    }

    private static async Task<string?> DownloadFile(string downloadUri, string downloadFileName)
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys Runner"); // GitHub API requires a user-agent

        // 3 Attempts to download the file
        for (int i = 0; i < 3; i++)
        {
            try
            {
                using FileStream fileStream = new(Path.Combine(_installerPath, downloadFileName), FileMode.Create, FileAccess.Write, FileShare.None);
                await (await httpClient.GetStreamAsync(downloadUri)).CopyToAsync(fileStream);
                return fileStream.Name;
            }
            catch
            {
            }
        }

        return null;
    }

    private static void CleanupUpdates()
    {
        if (!Path.Exists(_installerPath))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(_installerPath).Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(file);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr FindWindowW(string lpClassName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
