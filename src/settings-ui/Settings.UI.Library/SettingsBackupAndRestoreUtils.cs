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
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Settings.UI.Library
{
    public class SettingsBackupAndRestoreUtils
    {
        private class JsonMergeHelper
        {
            // code by ahsonkhan from https://stackoverflow.com/questions/58694837/system-text-json-merge-two-objects
            public static string Merge(string originalJson, string newContent)
            {
                var outputBuffer = new ArrayBufferWriter<byte>();

                using (JsonDocument jDoc1 = JsonDocument.Parse(originalJson))
                using (JsonDocument jDoc2 = JsonDocument.Parse(newContent))
                using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
                {
                    JsonElement root1 = jDoc1.RootElement;
                    JsonElement root2 = jDoc2.RootElement;

                    if (root1.ValueKind != JsonValueKind.Array && root1.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidOperationException($"The original JSON document to merge new content into must be a container type. Instead it is {root1.ValueKind}.");
                    }

                    if (root1.ValueKind != root2.ValueKind)
                    {
                        return originalJson;
                    }

                    if (root1.ValueKind == JsonValueKind.Array)
                    {
                        MergeArrays(jsonWriter, root1, root2);
                    }
                    else
                    {
                        MergeObjects(jsonWriter, root1, root2);
                    }
                }

                return Encoding.UTF8.GetString(outputBuffer.WrittenSpan);
            }

            private static void MergeObjects(Utf8JsonWriter jsonWriter, JsonElement root1, JsonElement root2)
            {
                jsonWriter.WriteStartObject();

                // Write all the properties of the first document.
                // If a property exists in both documents, either:
                // * Merge them, if the value kinds match (e.g. both are objects or arrays),
                // * Completely override the value of the first with the one from the second, if the value kind mismatches (e.g. one is object, while the other is an array or string),
                // * Or favor the value of the first (regardless of what it may be), if the second one is null (i.e. don't override the first).
                foreach (JsonProperty property in root1.EnumerateObject())
                {
                    string propertyName = property.Name;

                    JsonValueKind newValueKind;

                    if (root2.TryGetProperty(propertyName, out JsonElement newValue) && (newValueKind = newValue.ValueKind) != JsonValueKind.Null)
                    {
                        jsonWriter.WritePropertyName(propertyName);

                        JsonElement originalValue = property.Value;
                        JsonValueKind originalValueKind = originalValue.ValueKind;

                        if (newValueKind == JsonValueKind.Object && originalValueKind == JsonValueKind.Object)
                        {
                            MergeObjects(jsonWriter, originalValue, newValue); // Recursive call
                        }
                        else if (newValueKind == JsonValueKind.Array && originalValueKind == JsonValueKind.Array)
                        {
                            MergeArrays(jsonWriter, originalValue, newValue);
                        }
                        else
                        {
                            newValue.WriteTo(jsonWriter);
                        }
                    }
                    else
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                // Write all the properties of the second document that are unique to it.
                foreach (JsonProperty property in root2.EnumerateObject())
                {
                    if (!root1.TryGetProperty(property.Name, out _))
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                jsonWriter.WriteEndObject();
            }

            private static void MergeArrays(Utf8JsonWriter jsonWriter, JsonElement root1, JsonElement root2)
            {
                jsonWriter.WriteStartArray();

                // Write all the elements from both JSON arrays
                foreach (JsonElement element in root1.EnumerateArray())
                {
                    element.WriteTo(jsonWriter);
                }

                foreach (JsonElement element in root2.EnumerateArray())
                {
                    element.WriteTo(jsonWriter);
                }

                jsonWriter.WriteEndArray();
            }
        }

        public static void SetRegSettingsBackupAndRestoreItem(string itemName, string itemValue)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\PowerToys", true))
            {
                if (key != null)
                {
                    key.SetValue(itemName, itemValue);
                }
            }
        }

        public static string GetRegSettingsBackupAndRestoreRegItem(string itemName)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\PowerToys"))
            {
                if (key != null)
                {
                    var val = key.GetValue(itemName);
                    if (val != null)
                    {
                        return val.ToString();
                    }
                }
            }

            return null;
        }

        public static (bool success, string message) RestoreSettings(string appBasePath, string settingsBackupAndRestoreDir)
        {
            try
            {
                // Debugger.Launch();
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                {
                    return (false, $"BackupAndRestore_NoBackupSyncPath");
                }

                if (!Directory.Exists(settingsBackupAndRestoreDir))
                {
                    Logger.LogError($"Invalid settingsBackupAndRestoreDir {settingsBackupAndRestoreDir}");
                    return (false, "BackupAndRestore_InvalidBackupLocation");
                }

                var latestSettingsFolder = GetLatestSettingsFolder();

                if (latestSettingsFolder == null)
                {
                    return (false, $"BackupAndRestore_NoBackupsFound");
                }

                var allCurrentSettingsFiles = GetSettingsFiles(appBasePath);

                var backupRetoreSettings = JsonNode.Parse(File.ReadAllText("Settings\\backup_restore_settings.json"));

                // var restartAfterRestore = true;
                // backupRetoreSettings.RootElement.EnumerateObject()..TryGetProperty("RestartAfterRestore", out restartAfterRestore))
                var currentSettingsFiles = GetSettingsFiles(appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));
                var backupSettingsFiles = GetSettingsFiles(latestSettingsFolder).ToList().ToDictionary(x => x.Substring(latestSettingsFolder.Length));

                if (backupSettingsFiles.Count == 0)
                {
                    return (false, $"BackupAndRestore_NoBackupsFound");
                }

                var anyFilesUpdated = false;
                foreach (var currentFile in backupSettingsFiles)
                {
                    var relativePath = currentFile.Value.Substring(latestSettingsFolder.Length + 1);
                    var retoreFullPath = Path.Combine(appBasePath, relativePath);
                    var pathToFullRestore = Path.GetDirectoryName(retoreFullPath);

                    var settingsToRestore = GetExportVersion(backupRetoreSettings, currentFile.Key, currentFile.Value);
                    var settingsToRestoreJson = JsonSerializer.Serialize(settingsToRestore, new JsonSerializerOptions { WriteIndented = true });

                    if (currentSettingsFiles.ContainsKey(currentFile.Key))
                    {
                        // we have a setting file to restore to
                        using var currentSettingsFile = GetExportVersion(backupRetoreSettings, currentFile.Key, currentSettingsFiles[currentFile.Key]);
                        var currentSettingsFileJson = JsonSerializer.Serialize(currentSettingsFile, new JsonSerializerOptions { WriteIndented = true });

                        var settingsToRestoreChecksum = ChecksumUtil.GetStringChecksum(settingsToRestoreJson);
                        var currentSettingsFileChecksum = ChecksumUtil.GetStringChecksum(currentSettingsFileJson);

                        if (settingsToRestoreChecksum != currentSettingsFileChecksum)
                        {
                            // the settings file needs to be updated, update the real one with non-excluded stuff...
                            Logger.LogInfo($"Settings file {currentFile.Key} is different and is getting updated from backup");

                            var newCurrentSettingsFile = JsonMergeHelper.Merge(File.ReadAllText(currentSettingsFiles[currentFile.Key]), JsonSerializer.Serialize(settingsToRestore));
                            File.WriteAllText(currentSettingsFiles[currentFile.Key], newCurrentSettingsFile);
                            anyFilesUpdated = true;
                        }
                    }
                    else
                    {
                        // we don't have anything to merge this into, so... copy it all or skip it?
                        Logger.LogInfo($"Settings file {currentFile.Key} is in the backup but does not exist for restore");
                        var thisPathToRestore = Path.Combine(appBasePath, currentFile.Key.Substring(1));
                        if (Directory.Exists(Path.GetDirectoryName(thisPathToRestore)))
                        {
                            File.WriteAllText(thisPathToRestore, settingsToRestoreJson);
                        }

                        anyFilesUpdated = true;
                    }
                }

                if (anyFilesUpdated)
                {
                    var restartAfterRestore = (bool?)backupRetoreSettings!["RestartAfterRestore"];
                    if (!restartAfterRestore.HasValue || restartAfterRestore.Value)
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
                    return (false, $"BackupAndRestore_NothingToRestore");
                }
            }
            catch (Exception ex2)
            {
                return (false, $"There was an error: {ex2.Message}");
            }
        }

        private static string GetLatestSettingsFolder()
        {
            string settingsBackupAndRestoreDir = GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");

            if (settingsBackupAndRestoreDir == null)
            {
                return null;
            }

            var settingsBackups = Directory.GetDirectories(settingsBackupAndRestoreDir, "settings_*", SearchOption.TopDirectoryOnly).ToList().ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture));

            if (settingsBackups.Count == 0)
            {
                return null;
            }

            return settingsBackups.OrderByDescending(x => x.Key).FirstOrDefault().Value;
        }

        public static JsonNode GetLatestSettingsBackupManifest()
        {
            string settingsBackupAndRestoreDir = GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");

            if (settingsBackupAndRestoreDir == null)
            {
                return null;
            }

            var settingsBackups = Directory.GetDirectories(settingsBackupAndRestoreDir, "settings_*", SearchOption.TopDirectoryOnly).ToList().ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture));

            if (settingsBackups.Count == 0)
            {
                return null;
            }
            else
            {
                var folder = settingsBackups.OrderByDescending(x => x.Key).FirstOrDefault().Value;
                return JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
            }
        }

        private static bool SettingFileToUse(string name)
        {
            // FancyZones
            if (name.EndsWith("Keyboard Manager\\default.json", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith("settings.json", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else if (name.EndsWith("FancyZones\\layout-hotkeys.json", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith("FancyZones\\layout-templates.json", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }

            // else if (Regex.IsMatch(name, @"FancyZones\\(.*)?.json$", RegexOptions.IgnoreCase))
        }

        private static bool SettingFileToIgnore(string name)
        {
            return name.EndsWith("PowerToys\\log_settings.json", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith("PowerToys\\oobe_settings.json", StringComparison.InvariantCultureIgnoreCase);
        }

        private static string[] GetSettingsFiles(string path)
        {
            return Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Where(s => SettingFileToUse(s) && !SettingFileToIgnore(s)).ToArray();
        }

        public static (bool success, string message) BackupSettings(string appBasePath, string settingsBackupAndRestoreDir)
        {
            try
            {
                // Debugger.Launch();
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}");
                }

                if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                {
                    return (false, $"BackupAndRestore_NoBackupSyncPath");
                }

                if (!Path.IsPathRooted(settingsBackupAndRestoreDir))
                {
                    return (false, $"Invalid settingsBackupAndRestoreDir, not rooted");
                }

                if (settingsBackupAndRestoreDir.StartsWith(appBasePath, StringComparison.InvariantCultureIgnoreCase))
                {
                    // backup cannot be under app
                    Logger.LogError($"BackupSettings, backup cannot be under app");
                    return (false, "BackupAndRestore_InvalidBackupLocation");
                }

                var dirExists = Directory.Exists(settingsBackupAndRestoreDir);

                try
                {
                    if (!dirExists)
                    {
                        Directory.CreateDirectory(settingsBackupAndRestoreDir);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create dir {settingsBackupAndRestoreDir}", ex);
                    return (false, $"BackupAndRestore_BackupError");
                }

                var backupRetoreSettings = JsonNode.Parse(File.ReadAllText("Settings\\backup_restore_settings.json"));
                var currentSettingsFiles = GetSettingsFiles(appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));

                if (currentSettingsFiles.Count == 0)
                {
                    return (false, "BackupAndRestore_NoSettingsFilesFound");
                }

                var fullBackupDir = Path.Combine(settingsBackupAndRestoreDir, $"settings_{DateTime.UtcNow.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture)}");
                var latestSettingsFolder = GetLatestSettingsFolder();
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

                        Logger.LogInfo($"BackupSettings writing, {backupFullPath}.");
                        File.WriteAllText(backupFullPath, JsonSerializer.Serialize(newSettingsToBackup, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    else
                    {
                        skippedSettingsFiles.Add(currentFile.Key, (currentFile.Value, newSettingsToBackup));
                    }
                }

                if (!anyFileBackedUp)
                {
                    return (false, $"BackupAndRestore_NothingToBackup");
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

                    Logger.LogInfo($"BackupSettings writing, {backupFullPath}.");
                    File.WriteAllText(backupFullPath, JsonSerializer.Serialize(currentFile.Value.settings, new JsonSerializerOptions { WriteIndented = true }));
                }

                // add manifest
                var manifestData = new
                {
                    CreateDateTime = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture),
                    @Version = Helper.GetProductVersion(),
                    UpdatedFiles = updatedSettingsFiles.Keys.ToList(),
                    BackupSource = Environment.MachineName,
                    UnchangedFiles = skippedSettingsFiles.Keys.ToList(),
                };

                var manifest = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions() { WriteIndented = true });

                File.WriteAllText(Path.Combine(fullBackupDir, "manifest.json"), manifest);

                RemoveOldBackups(settingsBackupAndRestoreDir, 10, TimeSpan.FromDays(60));

                return (true, $"BackupAndRestore_BackupComplete");
            }
            catch (Exception ex2)
            {
                Logger.LogError($"There was an error: {ex2.Message}", ex2);
                return (false, $"BackupAndRestore_BackupError");
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
                // PowerToys Run hack fix-up
                var ptRunIgnoredSettings = GetPTRunIgnoredSettings(backupRetoreSettings);
                var ptrSettings = JsonNode.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));

                foreach (JsonObject plugintoChange in ptRunIgnoredSettings)
                {
                    foreach (JsonObject plugin in (JsonArray)ptrSettings["plugins"])
                    {
                        if (plugin["Id"].ToString() == plugintoChange["Id"].ToString())
                        {
                            foreach (var nameOfPropertyToRemove in (JsonArray)plugintoChange["Names"])
                            {
                                plugin.Remove(nameOfPropertyToRemove.ToString());
                            }
                        }
                    }
                }

                return JsonDocument.Parse(ptrSettings.ToJsonString());
            }
            else
            {
                return JsonDocument.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
            }
        }

        private static JsonArray GetPTRunIgnoredSettings(JsonNode backupRetoreSettings)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            if (backupRetoreSettings["IgnoredPTRunSettings"] != null)
            {
                return (JsonArray)backupRetoreSettings["IgnoredPTRunSettings"];
            }

            return new JsonArray();
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

        private static void RemoveOldBackups(string settingsBackupAndRestoreDir, int minNumberToKeep, TimeSpan deleteIfOlderThanTs)
        {
            var settingsBackups = Directory.GetDirectories(settingsBackupAndRestoreDir, "settings_*", SearchOption.TopDirectoryOnly).ToList().ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture));

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
