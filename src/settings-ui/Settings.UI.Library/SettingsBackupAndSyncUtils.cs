// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Settings.UI.Library
{
    public class SettingsBackupAndSyncUtils
    {
        public static void SetRegSettingsBackupAndSyncDir(string directory)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\PowerToys", true))
            {
                if (key != null)
                {
                    key.SetValue("SettingsBackupAndSyncDir", directory);
                }
            }
        }

        public static string GetRegSettingsBackupAndSyncDir()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\PowerToys"))
            {
                if (key != null)
                {
                    var val = key.GetValue("SettingsBackupAndSyncDir");
                    if (val != null)
                    {
                        return val.ToString();
                    }
                }
            }

            return null;
        }

        public static (bool success, string message) RestoreSettings(string appBasePath, string settingsBackupAndSyncDir)
        {
            if (!Directory.Exists(appBasePath))
            {
                return (false, $"Invalid appBasePath {appBasePath}");
            }

            if (!Directory.Exists(settingsBackupAndSyncDir))
            {
                return (false, $"Invalid settingsBackupAndSyncDir {settingsBackupAndSyncDir}");
            }

            var latestSettingsFolder = GetLatestSettingsFolder(settingsBackupAndSyncDir);

            if (latestSettingsFolder == null)
            {
                return (false, $"BackupAndSync_NoBackupsFound");
            }

            var allBackupSettingsFiles = GetSettingsFiles(latestSettingsFolder);
            var allCurrentSettingsFiles = GetSettingsFiles(appBasePath);

            var hasAnySettingChanged = HasAnySettingChanged(latestSettingsFolder, allBackupSettingsFiles, appBasePath, allCurrentSettingsFiles);
            if (!hasAnySettingChanged)
            {
                return (false, $"BackupAndSync_NothingToRestore");
            }

            try
            {
                foreach (var file in allBackupSettingsFiles)
                {
                    var relativePath = file.Substring(latestSettingsFolder.Length + 1);
                    var retoreFullPath = Path.Combine(appBasePath, relativePath);

                    var pathToFullRestore = Path.GetDirectoryName(retoreFullPath);

                    if (Directory.Exists(pathToFullRestore) && File.Exists(retoreFullPath))
                    {
                        var tempBackName = Path.GetTempFileName();

                        // make a copy to restore just in cast.
                        File.WriteAllBytes(tempBackName, File.ReadAllBytes(retoreFullPath));
                        File.Delete(retoreFullPath);
                        try
                        {
                            File.Copy(file, retoreFullPath);
                            File.Delete(tempBackName);
                        }
                        catch (Exception ex3)
                        {
                            // if we failed to copy the new file, try to restore the old
                            File.Copy(tempBackName, retoreFullPath);
                            return (false, $"Issue restoring {relativePath}, {ex3.Message}");
                        }
                    }
                }

                return (true, $"Found {allBackupSettingsFiles.Length} files.");
            }
            catch (Exception ex2)
            {
                return (false, $"There was an error: {ex2.Message}");
            }
        }

        private static string GetLatestSettingsFolder(string settingsBackupAndSyncDir)
        {
            var settingsBackups = Directory.GetDirectories(settingsBackupAndSyncDir, "settings_*", SearchOption.TopDirectoryOnly).ToList().ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture));

            if (settingsBackups.Count == 0)
            {
                return null;
            }

            return settingsBackups.OrderByDescending(x => x.Key).FirstOrDefault().Value;
        }

        private static bool SettingFileToUse(string name)
        {
            return name.EndsWith("Keyboard Manager\\default.json", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith("settings.json", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool SettingFileToIgnore(string name)
        {
            return name.EndsWith("PowerToys\\log_settings.json", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith("PowerToys\\oobe_settings.json", StringComparison.InvariantCultureIgnoreCase);
        }

        private static string[] GetSettingsFiles(string path)
        {
            return Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Where(s => SettingFileToUse(s) && !SettingFileToIgnore(s)).ToArray();
        }

        public static (bool success, string message) BackupSettings(string appBasePath, string settingsBackupAndSyncDir)
        {
            if (!Directory.Exists(appBasePath))
            {
                return (false, $"Invalid appBasePath {appBasePath}");
            }

            if (!Path.IsPathRooted(settingsBackupAndSyncDir))
            {
                return (false, $"Invalid settingsBackupAndSyncDir, not rooted");
            }

            if (settingsBackupAndSyncDir.StartsWith(appBasePath, StringComparison.InvariantCultureIgnoreCase))
            {
                // backup cannot be under app
                Logger.LogError($"BackupSettings, backup cannot be under app");
                return (false, "BackupAndSync_InvalidBackupLocation");
            }

            var dirExists = Directory.Exists(settingsBackupAndSyncDir);

            try
            {
                if (!dirExists)
                {
                    Directory.CreateDirectory(settingsBackupAndSyncDir);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create dir {settingsBackupAndSyncDir}, {ex.Message}");
                return (false, $"Failed to create dir {settingsBackupAndSyncDir}, {ex.Message}");
            }

            var allSettingsFiles = GetSettingsFiles(appBasePath);

            if (allSettingsFiles.Length == 0)
            {
                return (false, "BackupAndSync_NoSettingsFilesFound");
            }

            var fullBackupDir = Path.Combine(settingsBackupAndSyncDir, $"settings_{DateTime.UtcNow.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture)}");

            var latestSettingsFolder = GetLatestSettingsFolder(settingsBackupAndSyncDir);
            if (latestSettingsFolder != null)
            {
                var allLastSettingsFiles = GetSettingsFiles(latestSettingsFolder);

                var hasAnySettingChanged = HasAnySettingChanged(latestSettingsFolder, allLastSettingsFiles, appBasePath, allSettingsFiles);
                if (!hasAnySettingChanged)
                {
                    return (false, $"BackupAndSync_NothingToBackup");
                }
            }

            if (!Directory.Exists(fullBackupDir))
            {
                Directory.CreateDirectory(fullBackupDir);
            }
            else
            {
                return (false, $"Backup folder already exists?");
            }

            try
            {
                foreach (var file in allSettingsFiles)
                {
                    var relativePath = file.Substring(appBasePath.Length + 1);
                    var backupFullPath = Path.Combine(fullBackupDir, relativePath);

                    var pathToFullBackup = Path.GetDirectoryName(backupFullPath);
                    if (!Directory.Exists(pathToFullBackup))
                    {
                        Directory.CreateDirectory(pathToFullBackup);
                    }

                    File.Copy(file, backupFullPath);
                }

                RemoveOldBackups(settingsBackupAndSyncDir, 10, TimeSpan.FromDays(60));

                // var rm = new ResourceManager("Strings", typeof(Example).Assembly);
                return (true, $"BackupAndSync_BackupComplete");
            }
            catch (Exception ex2)
            {
                return (false, $"There was an error: {ex2.Message}");
            }
        }

        private static bool HasAnySettingChanged(string lastSettingsPath, string[] allLastSettingsFiles, string appBasePath, string[] fullBackupDir)
        {
            var sortFilesA = allLastSettingsFiles.ToList().OrderBy(f => f).ToArray();
            var sortFilesB = fullBackupDir.ToList().OrderBy(f => f).ToArray();

            if (sortFilesA.Length != sortFilesB.Length)
            {
                return true;
            }

            for (var i = 0; i < sortFilesA.Length; i++)
            {
                var lastSettingsFile = sortFilesA[i].Substring(lastSettingsPath.Length);
                var currentSettingsFile = sortFilesB[i].Substring(appBasePath.Length);

                if (!lastSettingsFile.Equals(currentSettingsFile, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var checkSomeA = ChecksumUtil.GetChecksum(sortFilesA[i]);
                var checkSomeB = ChecksumUtil.GetChecksum(sortFilesB[i]);
                if (checkSomeA != checkSomeB)
                {
                    return true;
                }
            }

            return false;
        }

        private static class ChecksumUtil
        {
            public static string GetChecksum(string filename)
            {
                using (var hasher = System.Security.Cryptography.HashAlgorithm.Create("SHA256"))
                {
                    using (var stream = System.IO.File.OpenRead(filename))
                    {
                        var hash = hasher.ComputeHash(stream);
                        return BitConverter.ToString(hash);
                    }
                }
            }
        }

        private static void RemoveOldBackups(string settingsBackupAndSyncDir, int minNumberToKeep, TimeSpan deleteIfOlderThanTs)
        {
            var settingsBackups = Directory.GetDirectories(settingsBackupAndSyncDir, "settings_*", SearchOption.TopDirectoryOnly).ToList().ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture));

            if (settingsBackups.Count <= minNumberToKeep)
            {
                return;
            }

            DateTime deleteIfOlder = DateTime.UtcNow.Subtract(deleteIfOlderThanTs);

            foreach (var item in settingsBackups)
            {
                var backupTime = DateTime.FromFileTimeUtc(item.Key);

                if (backupTime < deleteIfOlder)
                {
                    try
                    {
                        Directory.Delete(item.Value, true);
                    }
                    catch (Exception ex2)
                    {
                        Logger.LogError($"Failed to remove a setting backup folder ({item.Value}), because: ({ex2.Message})");
                    }
                }
            }
        }
    }
}
