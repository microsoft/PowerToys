// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualBasic.Logging;
using Newtonsoft.Json.Linq;

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
            try
            {
                // Debugger.Launch();
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndSyncDir))
                {
                    return (false, $"BackupAndSync_NoBackupSyncPath");
                }

                if (!Directory.Exists(settingsBackupAndSyncDir))
                {
                    Logger.LogError($"Invalid settingsBackupAndSyncDir {settingsBackupAndSyncDir}");
                    return (false, "BackupAndSync_InvalidBackupLocation");
                }

                var latestSettingsFolder = GetLatestSettingsFolder(settingsBackupAndSyncDir);

                if (latestSettingsFolder == null)
                {
                    return (false, $"BackupAndSync_NoBackupsFound");
                }

                var allCurrentSettingsFiles = GetSettingsFiles(appBasePath);

                var backupRetoreSettings = JObject.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")));
                var currentSettingsFiles = GetSettingsFiles(appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));
                var backupSettingsFiles = GetSettingsFiles(latestSettingsFolder).ToList().ToDictionary(x => x.Substring(latestSettingsFolder.Length));

                if (backupSettingsFiles.Count == 0)
                {
                    return (false, $"BackupAndSync_NoBackupsFound");
                }

                var anyFilesUpdated = false;
                foreach (var currentFile in backupSettingsFiles)
                {
                    var relativePath = currentFile.Value.Substring(latestSettingsFolder.Length + 1);
                    var retoreFullPath = Path.Combine(appBasePath, relativePath);
                    var pathToFullRestore = Path.GetDirectoryName(retoreFullPath);

                    var settingsToRestore = GetExportVersion(backupRetoreSettings, currentFile.Key, currentFile.Value);

                    if (currentSettingsFiles.ContainsKey(currentFile.Key))
                    {
                        // we have a setting file to restore to
                        var currentSettingsFile = GetExportVersion(backupRetoreSettings, currentFile.Key, currentSettingsFiles[currentFile.Key]);

                        var settingsToRestoreChecksum = ChecksumUtil.GetStringChecksum(settingsToRestore.ToString());
                        var currentSettingsFileChecksum = ChecksumUtil.GetStringChecksum(currentSettingsFile.ToString());

                        if (settingsToRestoreChecksum != currentSettingsFileChecksum)
                        {
                            // the settings file needs to be updated, update the real one with non-excluded stuff...
                            var realCurrentSettings = JObject.Parse(File.ReadAllText(currentSettingsFiles[currentFile.Key]));
                            var newCurrentSettingsFile = MergeJObjects(settingsToRestore, realCurrentSettings);
                            File.WriteAllText(currentSettingsFiles[currentFile.Key], newCurrentSettingsFile.ToString());

                            anyFilesUpdated = true;
                        }
                    }
                    else
                    {
                        // we don't have anything to merge this into, so... copy it all or skip it?
                    }
                }

                if (anyFilesUpdated)
                {
                    return (true, $"START APP");
                }
                else
                {
                    return (false, $"BackupAndSync_NothingToRestore");
                }
            }
            catch (Exception ex2)
            {
                return (false, $"There was an error: {ex2.Message}");
            }
        }

        private static JObject MergeJObjects(JObject a, JObject b)
        {
            b = JObject.Parse(b.ToString());
            b.Merge(a, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
            return b;
        }

        public static (bool success, string message) RestoreSettingsOld(string appBasePath, string settingsBackupAndSyncDir)
        {
            try
            {
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndSyncDir))
                {
                    return (false, $"BackupAndSync_NoBackupSyncPath");
                }

                if (!Directory.Exists(settingsBackupAndSyncDir))
                {
                    Logger.LogError($"Invalid settingsBackupAndSyncDir {settingsBackupAndSyncDir}");
                    return (false, "BackupAndSync_InvalidBackupLocation");
                }

                var latestSettingsFolder = GetLatestSettingsFolder(settingsBackupAndSyncDir);

                if (latestSettingsFolder == null)
                {
                    return (false, $"BackupAndSync_NoBackupsFound");
                }

                var allBackupSettingsFiles = GetSettingsFiles(latestSettingsFolder);
                var allCurrentSettingsFiles = GetSettingsFiles(appBasePath);

                var hasAnySettingChanged = ShouldDo("restore", latestSettingsFolder, appBasePath);
                if (!hasAnySettingChanged)
                {
                    return (false, $"BackupAndSync_NothingToRestore");
                }

                foreach (var file in allBackupSettingsFiles)
                {
                    var relativePath = file.Substring(latestSettingsFolder.Length + 1);
                    var retoreFullPath = Path.Combine(appBasePath, relativePath);

                    var pathToFullRestore = Path.GetDirectoryName(retoreFullPath);

                    if (!File.Exists(retoreFullPath))
                    {
                        Logger.LogInfo($"{retoreFullPath} does not exist, creating");
                        if (!Directory.Exists(pathToFullRestore))
                        {
                            Directory.CreateDirectory(pathToFullRestore);
                        }

                        File.WriteAllBytes(retoreFullPath, Array.Empty<byte>());
                    }

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
            try
            {
                // Debugger.Launch();
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndSyncDir))
                {
                    return (false, $"BackupAndSync_NoBackupSyncPath");
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
                    Logger.LogError($"Failed to create dir {settingsBackupAndSyncDir}", ex);
                    return (false, $"BackupAndSync_BackupError");
                }

                var backupRetoreSettings = JObject.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")));
                var currentSettingsFiles = GetSettingsFiles(appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));

                if (currentSettingsFiles.Count == 0)
                {
                    return (false, "BackupAndSync_NoSettingsFilesFound");
                }

                var fullBackupDir = Path.Combine(settingsBackupAndSyncDir, $"settings_{DateTime.UtcNow.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture)}");
                var latestSettingsFolder = GetLatestSettingsFolder(settingsBackupAndSyncDir);
                var backupSettingsFiles = new Dictionary<string, string>();
                if (latestSettingsFolder != null)
                {
                    backupSettingsFiles = GetSettingsFiles(latestSettingsFolder).ToList().ToDictionary(x => x.Substring(latestSettingsFolder.Length));
                }

                var anyFileBackedUp = false;
                var skippedSettingsFiles = new Dictionary<string, (string path, JObject settings)>();
                var updatedSettingsFiles = new Dictionary<string, (string path, JObject settings)>();
                foreach (var currentFile in currentSettingsFiles)
                {
                    // need to check and back this up;
                    var newSettingsToBackup = GetExportVersion(backupRetoreSettings, currentFile.Key, currentFile.Value);

                    var doBackup = false;
                    if (backupSettingsFiles.ContainsKey(currentFile.Key))
                    {
                        var newSettingsChecksum = ChecksumUtil.GetStringChecksum(newSettingsToBackup.ToString());
                        var lastSettingsChecksum = ChecksumUtil.GetFileChecksum(backupSettingsFiles[currentFile.Key]);

                        if (newSettingsChecksum != lastSettingsChecksum)
                        {
                            doBackup = true;
                            Logger.LogInfo($"ShouldDo, {currentFile.Value} content is different.");
                        }
                    }
                    else
                    {
                        // this has never been backed up
                        Logger.LogInfo($"ShouldDo, {currentFile.Value} does not exists.");
                        doBackup = true;
                    }

                    if (doBackup)
                    {
                        updatedSettingsFiles.Add(currentFile.Key, (currentFile.Value, newSettingsToBackup));
                        anyFileBackedUp = true;

                        var relativePath = currentFile.Value.Substring(appBasePath.Length + 1);
                        var backupFullPath = Path.Combine(fullBackupDir, relativePath);

                        if (!Directory.Exists(fullBackupDir))
                        {
                            Directory.CreateDirectory(fullBackupDir);
                        }

                        if (!Directory.Exists(Path.GetDirectoryName(backupFullPath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(backupFullPath));
                        }

                        File.WriteAllText(backupFullPath, newSettingsToBackup.ToString());
                    }
                    else
                    {
                        // save this just in case we need to add it
                        skippedSettingsFiles.Add(currentFile.Key, (currentFile.Value, newSettingsToBackup));
                    }
                }

                if (!anyFileBackedUp)
                {
                    return (false, $"BackupAndSync_NothingToBackup");
                }

                // add skipped.
                foreach (var currentFile in skippedSettingsFiles)
                {
                    var relativePath = currentFile.Value.path.Substring(appBasePath.Length + 1);
                    var backupFullPath = Path.Combine(fullBackupDir, relativePath);

                    if (!Directory.Exists(fullBackupDir))
                    {
                        Directory.CreateDirectory(fullBackupDir);
                    }

                    if (!Directory.Exists(Path.GetDirectoryName(backupFullPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupFullPath));
                    }

                    File.WriteAllText(backupFullPath, currentFile.Value.settings.ToString());
                }

                // add manifest
                var manifestData = new
                {
                    CreateDateTime = DateTime.UtcNow,
                    @Version = Helper.GetProductVersion(),
                    UpdatedFiles = updatedSettingsFiles.Keys.ToList(),
                    NewFiles = skippedSettingsFiles.Keys.ToList(),
                };

                var manifest = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions() { WriteIndented = true });

                File.WriteAllText(Path.Combine(fullBackupDir, "manifest.json"), manifest);

                RemoveOldBackups(settingsBackupAndSyncDir, 10, TimeSpan.FromDays(60));

                return (true, $"BackupAndSync_BackupComplete");
            }
            catch (Exception ex2)
            {
                Logger.LogError($"There was an error: {ex2.Message}", ex2);
                return (false, $"BackupAndSync_BackupError");
            }
        }

        public static (bool success, string message) BackupSettingsOld(string appBasePath, string settingsBackupAndSyncDir)
        {
            try
            {
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndSyncDir))
                {
                    return (false, $"BackupAndSync_NoBackupSyncPath");
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
                    Logger.LogError($"Failed to create dir {settingsBackupAndSyncDir}", ex);
                    return (false, $"BackupAndSync_BackupError");
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

                    var hasAnySettingChanged = ShouldDo("backup", latestSettingsFolder, appBasePath);
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

                // add manifest
                var manifest = JsonSerializer.Serialize(new { CreateDateTime = DateTime.UtcNow, @Version = Helper.GetProductVersion() }, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(Path.Combine(fullBackupDir, "manifest.json"), manifest);

                RemoveOldBackups(settingsBackupAndSyncDir, 10, TimeSpan.FromDays(60));

                // var rm = new ResourceManager("Strings", typeof(Example).Assembly);
                return (true, $"BackupAndSync_BackupComplete");
            }
            catch (Exception ex2)
            {
                Logger.LogError($"There was an error: {ex2.Message}", ex2);
                return (false, $"BackupAndSync_BackupError");
            }
        }

        private static bool ShouldDo(string purpose, string lastSettingsPath, string appBasePath)
        {
            var backupRetoreSettings = JObject.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")));
            var backupSettingsFiles = GetSettingsFiles(lastSettingsPath).ToList().ToDictionary(x => x.Substring(lastSettingsPath.Length));
            var currentSettingsFiles = GetSettingsFiles(appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));

            if (purpose == "backup")
            {
                foreach (var currentFile in currentSettingsFiles)
                {
                    if (backupSettingsFiles.ContainsKey(currentFile.Key))
                    {
                        var exportJson = GetExportVersion(backupRetoreSettings, currentFile.Key, currentFile.Value);

                        // var currentFileChecksum = ChecksumUtil.GetFileChecksum(currentFile.Value);
                        var currentFileChecksum = ChecksumUtil.GetStringChecksum(exportJson.ToString());
                        var backupFileChecksum = ChecksumUtil.GetFileChecksum(backupSettingsFiles[currentFile.Key]);

                        if (currentFileChecksum != backupFileChecksum)
                        {
                            Logger.LogInfo($"ShouldDo, {currentFile.Value} content is different.");
                            return true;
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"ShouldDo, backup is missing file {currentFile.Key}.");
                        return true;
                    }
                }
            }
            else
            {
                foreach (var backupFile in backupSettingsFiles)
                {
                    if (currentSettingsFiles.ContainsKey(backupFile.Key))
                    {
                        var backupFileChecksum = ChecksumUtil.GetFileChecksum(backupFile.Value);
                        var currentFileChecksum = ChecksumUtil.GetFileChecksum(currentSettingsFiles[backupFile.Key]);
                        if (backupFileChecksum != currentFileChecksum)
                        {
                            Logger.LogInfo($"ShouldDo, {backupFile.Value} content is different.");
                            return true;
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"ShouldDo, restore has extra file {backupFile.Key}, that's OK");
                        return true;
                    }
                }
            }

            return false;
        }

        public static JObject GetExportVersion(JObject backupRetoreSettings, string settingFileKey, string settingsFileName)
        {
            if (settingFileKey.StartsWith("\\", StringComparison.OrdinalIgnoreCase))
            {
                settingFileKey = settingFileKey.Substring(1);
            }

            var ignoredSettings = GetIgnoredSettings(backupRetoreSettings, settingFileKey);
            var settingsFile = JObject.Parse(File.ReadAllText(settingsFileName));

            if (ignoredSettings.Length == 0)
            {
                return settingsFile;
            }

            foreach (var property in settingsFile.Properties().ToList())
            {
                if (ignoredSettings.Contains(property.Name))
                {
                    settingsFile.Remove(property.Name);
                }
            }

            return settingsFile;
        }

        private static string[] GetIgnoredSettings(JObject backupRetoreSettings, string settingFileKey)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            var ignoredSettings = backupRetoreSettings["IgnoredSettings"];

            if (ignoredSettings != null && ignoredSettings[settingFileKey] != null)
            {
                ignoredSettings = ignoredSettings[settingFileKey];
                if (ignoredSettings != null)
                {
                    return ((JArray)ignoredSettings).ToObject<string[]>();
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        private static class ChecksumUtil
        {
            public static string GetFileChecksum(string filename)
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

            public static string GetStringChecksum(string s)
            {
                using var hasher = System.Security.Cryptography.HashAlgorithm.Create("SHA256");
                using var stream = new MemoryStream();
                using var writer = new StreamWriter(stream);
                writer.Write(s);
                writer.Flush();
                stream.Position = 0;
                var hash = hasher.ComputeHash(stream);
                return BitConverter.ToString(hash);
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
