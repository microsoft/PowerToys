// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Update
{
    [SupportedOSPlatform("windows")]
    public static class UpdateSettingsHelper
    {
        private static Thread? _updateThread;

        private const string INSTALLERFILENAME = "powertoyssetup";
        private const string USERINSTALLERFILENAME = "powertoysusersetup";

        public static void TriggerUpdateCheck(Action<string> doIfUpdateAvailable)
        {
            if (_updateThread is not null && _updateThread.IsAlive)
            {
                return;
            }

            _updateThread = new Thread(async () =>
            {
                UpdateInfo updateInfo = await GetUpdateAvailableInfo();
                switch (updateInfo)
                {
                    case UpdateInfo.UpdateCheckFailed ucf:
                        ProcessUpdateCheckFailed(ucf);
                        break;
                    case UpdateInfo.UpdateAvailable ua:
                        ProcessUpdateAvailable(ua);
                        doIfUpdateAvailable($"v{ua.AvailableVersion.Major}.{ua.AvailableVersion.Minor}.{ua.AvailableVersion.Build}");
                        break;
                    case UpdateInfo.NoUpdateAvailable:
                        ProcessNoUpdateAvailable();
                        break;
                }
            });

            _updateThread.Start();
        }

        public static string GetLastCheckedDate()
        {
            UpdatingSettings updatingSettings = UpdatingSettings.LoadSettings();
            if (long.TryParse(updatingSettings.LastCheckedDate, out long lastCheckedDateSeconds))
            {
                DateTimeOffset lastCheckedDate = DateTimeOffset.FromUnixTimeSeconds(lastCheckedDateSeconds);
                return lastCheckedDate.ToString("g", CultureInfo.CurrentCulture);
            }

            return string.Empty;
        }

        internal record UpdateInfo
        {
            private UpdateInfo()
            {
            }

            public sealed record NoUpdateAvailable : UpdateInfo;

            public sealed record UpdateAvailable(Uri ReleasePageUri, Version AvailableVersion, Uri InstallerDownloadUrl, string InstallerFilename) : UpdateInfo;

            public sealed record UpdateCheckFailed(Exception Exception) : UpdateInfo;
        }

        internal static async Task<UpdateInfo> GetUpdateAvailableInfo()
        {
            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (currentVersion is null)
            {
                // Todo: Log
                return new UpdateInfo.NoUpdateAvailable();
            }

            /*if (currentVersion is { Major: 0, Minor: 0 })
            {
                // Pre-release or local build, skip update check
                return new UpdateInfo.NoUpdateAvailable();
            }*/

            try
            {
                HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys Runner"); // GitHub API requires a user-agent
                Stream body = await httpClient.GetStreamAsync("https://api.github.com/repos/microsoft/PowerToys/releases/latest").ConfigureAwait(false);
                JsonElement releaseObject = (await JsonDocument.ParseAsync(body)).RootElement;
                Version latestVersion = new(releaseObject.GetProperty("tag_name").GetString()?.TrimStart('V', 'v') ?? throw new FormatException("The \"tag_name\" field could not be found"));
                string architectureString = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    _ => throw new InvalidDataException("Unknown architecture"),
                };

                if (latestVersion > currentVersion)
                {
                    Uri releasePageUri = new(releaseObject.GetProperty("html_url").GetString() ?? throw new FormatException("The \"html_url\" field could not be found"));

                    string requiredFilename = GetInstallScope() == InstallScope.PerMachine ? INSTALLERFILENAME : USERINSTALLERFILENAME;

                    Uri? installerDownloadUrl = null;
                    string? installerFilename = null;

                    foreach (JsonElement asset in releaseObject.GetProperty("assets").EnumerateArray())
                    {
                        string? name = asset.GetProperty("name").GetString();
                        string? browserDownloadUrl = asset.GetProperty("browser_download_url").GetString();

                        if (name is null
                            || browserDownloadUrl is null
                            || !name.Contains(requiredFilename, StringComparison.InvariantCultureIgnoreCase)
                            || !name.Contains(".exe", StringComparison.InvariantCultureIgnoreCase)
                            || !name.Contains(architectureString, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        installerDownloadUrl = new Uri(browserDownloadUrl);
                        installerFilename = name;
                        break;
                    }

                    return installerDownloadUrl is null || installerFilename is null
                        ? new UpdateInfo.UpdateCheckFailed(new InvalidDataException("No installer found in GitHub release"))
                        : new UpdateInfo.UpdateAvailable(releasePageUri, latestVersion, installerDownloadUrl, installerFilename);
                }

                return new UpdateInfo.NoUpdateAvailable();
            }
            catch (Exception e)
            {
                return new UpdateInfo.UpdateCheckFailed(e);
            }
        }

        private enum InstallScope
        {
            PerMachine,
            PerUser,
        }

        [SupportedOSPlatform("windows")]
        private static InstallScope GetInstallScope()
        {
            if (Registry.LocalMachine.OpenSubKey(@"Software\Classes\powertoys\", false) is not RegistryKey machineKey)
            {
                if (Registry.CurrentUser.OpenSubKey(@"Software\Classes\powertoys\", false) is not RegistryKey userKey)
                {
                    // Both keys are missing
                    return InstallScope.PerMachine;
                }

                if (userKey.GetValue("InstallScope") is not string installScope)
                {
                    userKey.Close();
                    return InstallScope.PerMachine;
                }

                userKey.Close();

                return installScope.Contains("perUser") ? InstallScope.PerUser : InstallScope.PerMachine;
            }

            machineKey.Close();

            return InstallScope.PerMachine;
        }

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys");

        private static readonly string _updatingSettingsFile = Path.Combine(_settingsPath, "UpdateState.json");

        private static void ProcessUpdateAvailable(UpdateInfo.UpdateAvailable updateAvailable)
        {
            UpdatingSettings updatingSettings = UpdatingSettings.LoadSettings();
            Console.WriteLine($"Update available: {updateAvailable.AvailableVersion}");

            updatingSettings.State = UpdatingSettings.UpdatingState.ReadyToDownload;
            updatingSettings.ReleasePageLink = updateAvailable.ReleasePageUri.ToString();
            updatingSettings.DownloadedInstallerFilename = updateAvailable.InstallerFilename;
            updatingSettings.ReleasePageLink = updateAvailable.ReleasePageUri.ToString();
            updatingSettings.LastCheckedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

            File.WriteAllText(_updatingSettingsFile, updatingSettings.ToJsonString());
        }

        internal static void ProcessNoUpdateAvailable()
        {
            UpdatingSettings updatingSettings = UpdatingSettings.LoadSettings();

            updatingSettings.State = UpdatingSettings.UpdatingState.UpToDate;
            updatingSettings.ReleasePageLink = string.Empty;
            updatingSettings.DownloadedInstallerFilename = string.Empty;
            updatingSettings.ReleasePageLink = string.Empty;
            updatingSettings.LastCheckedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(_updatingSettingsFile, updatingSettings.ToJsonString());
        }

        private static void ProcessUpdateCheckFailed(UpdateInfo.UpdateCheckFailed updateCheckFailed)
        {
            // Todo: Log failed attempt
            UpdatingSettings updatingSettings = UpdatingSettings.LoadSettings();

            updatingSettings.State = UpdatingSettings.UpdatingState.NetworkError;
            updatingSettings.ReleasePageLink = string.Empty;
            updatingSettings.DownloadedInstallerFilename = string.Empty;
            updatingSettings.ReleasePageLink = string.Empty;
            updatingSettings.LastCheckedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(_updatingSettingsFile, updatingSettings.ToJsonString());
        }

        internal static void SetUpdateState(UpdatingSettings.UpdatingState state)
        {
            UpdatingSettings updatingSettings = UpdatingSettings.LoadSettings();

            updatingSettings.State = state;
            File.WriteAllText(_updatingSettingsFile, updatingSettings.ToJsonString());
        }

        internal static UpdatingSettings.UpdatingState GetUpdateState() => UpdatingSettings.LoadSettings().State;
    }
}
