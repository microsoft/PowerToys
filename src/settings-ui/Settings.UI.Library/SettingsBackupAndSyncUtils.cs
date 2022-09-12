// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

                var backupRetoreSettings = JsonNode.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")));

                var forecastNode = JsonNode.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")))!;

                // var restartAfterRestore = true;
                // backupRetoreSettings.RootElement.EnumerateObject()..TryGetProperty("RestartAfterRestore", out restartAfterRestore))
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
                        using var currentSettingsFile = GetExportVersion(backupRetoreSettings, currentFile.Key, currentSettingsFiles[currentFile.Key]);

                        var settingsToRestoreJson = JsonSerializer.Serialize(settingsToRestore, new JsonSerializerOptions { WriteIndented = true });
                        var currentSettingsFileJson = JsonSerializer.Serialize(currentSettingsFile, new JsonSerializerOptions { WriteIndented = true });

                        var settingsToRestoreChecksum = ChecksumUtil.GetStringChecksum(settingsToRestoreJson);
                        var currentSettingsFileChecksum = ChecksumUtil.GetStringChecksum(currentSettingsFileJson);

                        if (settingsToRestoreChecksum != currentSettingsFileChecksum)
                        {
                            // the settings file needs to be updated, update the real one with non-excluded stuff...
                            Logger.LogInfo($"Settings file {currentFile.Key} is different and is getting updated from backup");
                            Logger.LogInfo($"X: settingsToRestoreChecksum:{settingsToRestoreChecksum},  currentSettingsFileJson:{currentSettingsFileJson}");

                            using var realCurrentSettings = JsonDocument.Parse(File.ReadAllText(currentSettingsFiles[currentFile.Key]));
                            using var newCurrentSettingsFile = MergeJObjects(settingsToRestore, realCurrentSettings);
                            File.WriteAllText(currentSettingsFiles[currentFile.Key], JsonSerializer.Serialize(newCurrentSettingsFile));
                            anyFilesUpdated = true;
                        }
                    }
                    else
                    {
                        // we don't have anything to merge this into, so... copy it all or skip it?
                        Logger.LogInfo($"Settings file {currentFile.Key} is in the backup but does not exist for restore");
                    }
                }

                if (anyFilesUpdated)
                {
                    var temperatureNode = (bool?)backupRetoreSettings!["RestartAfterRestore"];
                    if (!temperatureNode.HasValue || temperatureNode.Value)
                    {
                        return (true, $"RESTART APP");
                    }
                    else
                    {
                        return (false, $"RESTART APP");
                    }
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

        private static JsonDocument MergeJObjects(JsonDocument newContent, JsonDocument origDoc)
        {
            var outputBuffer = new ArrayBufferWriter<byte>();

            using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
            {
                JsonElement root1 = origDoc.RootElement;
                JsonElement root2 = newContent.RootElement;

                jsonWriter.WriteStartObject();

                // Write all the properties of the first document that don't conflict with the second
                foreach (JsonProperty property in root1.EnumerateObject().OrderBy(p => p.Name))
                {
                    if (!root2.TryGetProperty(property.Name, out _))
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                // Write all the properties of the second document (including those that are duplicates which were skipped earlier)
                // The property values of the second document completely override the values of the first
                foreach (JsonProperty property in root2.EnumerateObject().OrderBy(p => p.Name))
                {
                    property.WriteTo(jsonWriter);
                }

                jsonWriter.WriteEndObject();
            }

            return JsonDocument.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
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

                var backupRetoreSettings = JsonNode.Parse(File.ReadAllText(Path.Combine(appBasePath, "backup-restore_settings.json")));
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
                var skippedSettingsFiles = new Dictionary<string, (string path, JsonDocument settings)>();
                var updatedSettingsFiles = new Dictionary<string, (string path, JsonDocument settings)>();
                foreach (var currentFile in currentSettingsFiles)
                {
                    // need to check and back this up;
                    var newSettingsToBackup = GetExportVersion(backupRetoreSettings, currentFile.Key, currentFile.Value);

                    var doBackup = false;
                    if (backupSettingsFiles.ContainsKey(currentFile.Key))
                    {
                        var lastSettingsFileDoc = GetExportVersion(backupRetoreSettings, currentFile.Key, backupSettingsFiles[currentFile.Key]);

                        var newSettingsChecksum = ChecksumUtil.GetStringChecksum(JsonSerializer.Serialize(newSettingsToBackup, new JsonSerializerOptions { WriteIndented = true }));
                        var lastSettingsChecksum = ChecksumUtil.GetStringChecksum(JsonSerializer.Serialize(lastSettingsFileDoc, new JsonSerializerOptions { WriteIndented = true }));

                        if (newSettingsChecksum != lastSettingsChecksum)
                        {
                            doBackup = true;
                            Logger.LogInfo($"BackupSettings, {currentFile.Value} content is different.");
                        }
                    }
                    else
                    {
                        // this has never been backed up
                        Logger.LogInfo($"BackupSettings, {currentFile.Value} does not exists.");
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

                        Logger.LogInfo($"BackupSettings writting, {backupFullPath}.");
                        File.WriteAllText(backupFullPath, JsonSerializer.Serialize(newSettingsToBackup, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    else
                    {
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

                    Logger.LogInfo($"BackupSettings writting, {backupFullPath}.");
                    File.WriteAllText(backupFullPath, JsonSerializer.Serialize(currentFile.Value.settings, new JsonSerializerOptions { WriteIndented = true }));
                }

                // add manifest
                var manifestData = new
                {
                    CreateDateTime = DateTime.UtcNow,
                    @Version = Helper.GetProductVersion(),
                    UpdatedFiles = updatedSettingsFiles.Keys.ToList(),
                    UnchangedFiles = skippedSettingsFiles.Keys.ToList(),
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

        public static JsonDocument GetExportVersion(JsonNode backupRetoreSettings, string settingFileKey, string settingsFileName)
        {
            var ignoredSettings = GetIgnoredSettings(backupRetoreSettings, settingFileKey);
            var settingsFile = JsonDocument.Parse(File.ReadAllText(settingsFileName));

            var outputBuffer = new ArrayBufferWriter<byte>();

            using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
            {
                jsonWriter.WriteStartObject();
                foreach (var property in settingsFile.RootElement.EnumerateObject().OrderBy(p => p.Name))
                {
                    if (!ignoredSettings.Contains(property.Name))
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                jsonWriter.WriteEndObject();
            }

            if (settingFileKey.Equals("\\PowerToys Run\\settings.json", StringComparison.OrdinalIgnoreCase))
            {
                // hack fix-up
                var json = Encoding.UTF8.GetString(outputBuffer.WrittenSpan);
                json = json.Replace("VSCodeWorkspaces\\\\Images\\\\code-", "VSCodeWorkspace\\\\Images\\\\code-");
                return JsonDocument.Parse(json);
            }
            else
            {
                return JsonDocument.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
            }
        }

        private static string[] GetIgnoredSettings(JsonNode backupRetoreSettings, string settingFileKey)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            if (settingFileKey.StartsWith("\\", StringComparison.OrdinalIgnoreCase))
            {
                settingFileKey = settingFileKey.Substring(1);
            }

            if (backupRetoreSettings["IgnoredSettings"] != null)
            {
                if (backupRetoreSettings["IgnoredSettings"][settingFileKey] != null)
                {
                    var settingsArray = (JsonArray)backupRetoreSettings["IgnoredSettings"][settingFileKey];

                    Console.WriteLine("settingsArray " + settingsArray.GetType().FullName);

                    var settingsList = new List<string>();

                    foreach (var setting in settingsArray)
                    {
                        settingsList.Add(setting.ToString());
                    }

                    return settingsList.ToArray();
                }
                else
                {
                    return Array.Empty<string>();
                }
            }

            return Array.Empty<string>();
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
                        Logger.LogInfo($"RemoveOldBackups killing {item.Value}");
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
