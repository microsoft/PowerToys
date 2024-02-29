// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SettingsBackupAndRestoreUtils
    {
        private static SettingsBackupAndRestoreUtils instance;
        private (bool Success, string Severity, bool LastBackupExists, DateTime? LastRan) lastBackupSettingsResults;
        private static object backupSettingsInternalLock = new object();
        private static object removeOldBackupsLock = new object();

        public DateTime LastBackupStartTime { get; set; }

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private SettingsBackupAndRestoreUtils()
        {
            LastBackupStartTime = DateTime.MinValue;
        }

        public static SettingsBackupAndRestoreUtils Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SettingsBackupAndRestoreUtils();
                }

                return instance;
            }
        }

        private sealed class JsonMergeHelper
        {
            // mostly from https://stackoverflow.com/questions/58694837/system-text-json-merge-two-objects
            // but with some update to prevent array item duplicates
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
                        MergeArrays(jsonWriter, root1, root2, false);
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
                            MergeArrays(jsonWriter, originalValue, newValue, false);
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

            private static void MergeArrays(Utf8JsonWriter jsonWriter, JsonElement root1, JsonElement root2, bool allowDupes)
            {
                // just does one level!!!
                jsonWriter.WriteStartArray();

                if (allowDupes)
                {
                    // Write all the elements from both JSON arrays
                    foreach (JsonElement element in root1.EnumerateArray())
                    {
                        element.WriteTo(jsonWriter);
                    }

                    foreach (JsonElement element in root2.EnumerateArray())
                    {
                        element.WriteTo(jsonWriter);
                    }
                }
                else
                {
                    var arrayItems = new HashSet<string>();
                    foreach (JsonElement element in root1.EnumerateArray())
                    {
                        element.WriteTo(jsonWriter);
                        arrayItems.Add(element.ToString());
                    }

                    foreach (JsonElement element in root2.EnumerateArray())
                    {
                        if (!arrayItems.Contains(element.ToString()))
                        {
                            element.WriteTo(jsonWriter);
                        }
                    }
                }

                jsonWriter.WriteEndArray();
            }
        }

        private static bool TryCreateDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return true;
                }
            }
            catch (Exception ex3)
            {
                Logger.LogError($"There was an error in TryCreateDirectory {path}: {ex3.Message}", ex3);
                return false;
            }

            return true;
        }

        private static bool TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
            }
            catch (Exception ex3)
            {
                Logger.LogError($"There was an error in TryDeleteDirectory {path}: {ex3.Message}", ex3);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Method <c>SetRegSettingsBackupAndRestoreItem</c> helper method to write to the registry.
        /// </summary>
        public static void SetRegSettingsBackupAndRestoreItem(string itemName, string itemValue)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft", true))
            {
                var ptKey = key.OpenSubKey("PowerToys", true);
                if (ptKey != null)
                {
                    ptKey.SetValue(itemName, itemValue);
                }
                else
                {
                    var newPtKey = key.CreateSubKey("PowerToys");
                    newPtKey.SetValue(itemName, itemValue);
                }
            }
        }

        /// <summary>
        /// Method <c>GetRegSettingsBackupAndRestoreRegItem</c> helper method to read from the registry.
        /// </summary>
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

        /// <summary>
        /// Method <c>RestoreSettings</c> returns a folder that has the latest backup in it.
        /// </summary>
        /// <returns>
        /// A tuple that indicates if the backup was done or not, and a message.
        /// The message usually is a localized reference key.
        /// </returns>
        public (bool Success, string Message, string Severity) RestoreSettings(string appBasePath, string settingsBackupAndRestoreDir)
        {
            try
            {
                // verify inputs
                if (!Directory.Exists(appBasePath))
                {
                    return (false, $"Invalid appBasePath {appBasePath}", "Error");
                }

                if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                {
                    return (false, $"General_SettingsBackupAndRestore_NoBackupSyncPath", "Error");
                }

                if (!Directory.Exists(settingsBackupAndRestoreDir))
                {
                    Logger.LogError($"Invalid settingsBackupAndRestoreDir {settingsBackupAndRestoreDir}");
                    return (false, "General_SettingsBackupAndRestore_InvalidBackupLocation", "Error");
                }

                var latestSettingsFolder = GetLatestSettingsFolder();

                if (latestSettingsFolder == null)
                {
                    return (false, $"General_SettingsBackupAndRestore_NoBackupsFound", "Warning");
                }

                // get data needed for process
                var backupRestoreSettings = JsonNode.Parse(GetBackupRestoreSettingsJson());
                var currentSettingsFiles = GetSettingsFiles(backupRestoreSettings, appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));
                var backupSettingsFiles = GetSettingsFiles(backupRestoreSettings, latestSettingsFolder).ToList().ToDictionary(x => x.Substring(latestSettingsFolder.Length));

                if (backupSettingsFiles.Count == 0)
                {
                    return (false, $"General_SettingsBackupAndRestore_NoBackupsFound", "Warning");
                }

                var anyFilesUpdated = false;

                foreach (var currentFile in backupSettingsFiles)
                {
                    var relativePath = currentFile.Value.Substring(latestSettingsFolder.Length + 1);
                    var restoreFullPath = Path.Combine(appBasePath, relativePath);
                    var settingsToRestoreJson = GetExportVersion(backupRestoreSettings, currentFile.Key, currentFile.Value);

                    if (currentSettingsFiles.TryGetValue(currentFile.Key, out string value))
                    {
                        // we have a setting file to restore to
                        var currentSettingsFileJson = GetExportVersion(backupRestoreSettings, currentFile.Key, value);

                        if (JsonNormalizer.Normalize(settingsToRestoreJson) != JsonNormalizer.Normalize(currentSettingsFileJson))
                        {
                            // the settings file needs to be updated, update the real one with non-excluded stuff...
                            Logger.LogInfo($"Settings file {currentFile.Key} is different and is getting updated from backup");

                            // we needed a new "CustomRestoreSettings" for now, to overwrite because some settings don't merge well (like KBM shortcuts)
                            var overwrite = false;
                            if (backupRestoreSettings["CustomRestoreSettings"] != null && backupRestoreSettings["CustomRestoreSettings"][currentFile.Key] != null)
                            {
                                var customRestoreSettings = backupRestoreSettings["CustomRestoreSettings"][currentFile.Key];
                                overwrite = customRestoreSettings["overwrite"] != null && (bool)customRestoreSettings["overwrite"];
                            }

                            if (overwrite)
                            {
                                File.WriteAllText(currentSettingsFiles[currentFile.Key], settingsToRestoreJson);
                            }
                            else
                            {
                                var newCurrentSettingsFile = JsonMergeHelper.Merge(File.ReadAllText(currentSettingsFiles[currentFile.Key]), settingsToRestoreJson);
                                File.WriteAllText(currentSettingsFiles[currentFile.Key], newCurrentSettingsFile);
                            }

                            anyFilesUpdated = true;
                        }
                    }
                    else
                    {
                        // we don't have anything to merge this into, we need to use it as is
                        Logger.LogInfo($"Settings file {currentFile.Key} is in the backup but does not exist for restore");

                        var thisPathToRestore = Path.Combine(appBasePath, currentFile.Key.Substring(1));
                        TryCreateDirectory(Path.GetDirectoryName(thisPathToRestore));
                        File.WriteAllText(thisPathToRestore, settingsToRestoreJson);
                        anyFilesUpdated = true;
                    }
                }

                if (anyFilesUpdated)
                {
                    // something was changed do we need to return true to indicate a restart is needed.
                    var restartAfterRestore = (bool?)backupRestoreSettings!["RestartAfterRestore"];
                    if (!restartAfterRestore.HasValue || restartAfterRestore.Value)
                    {
                        return (true, $"RESTART APP", "Success");
                    }
                    else
                    {
                        return (false, $"RESTART APP", "Success");
                    }
                }
                else
                {
                    return (false, $"General_SettingsBackupAndRestore_NothingToRestore", "Informational");
                }
            }
            catch (Exception ex2)
            {
                Logger.LogError("Error in RestoreSettings, " + ex2.ToString());
                return (false, $"General_SettingsBackupAndRestore_BackupError", "Error");
            }
        }

        /// <summary>
        /// Method <c>GetSettingsBackupAndRestoreDir</c> returns the path the backup and restore location.
        /// </summary>
        /// <remarks>
        /// This will return a default location based on user documents if non is set.
        /// </remarks>
        public string GetSettingsBackupAndRestoreDir()
        {
            string settingsBackupAndRestoreDir = GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");
            if (settingsBackupAndRestoreDir == null)
            {
                settingsBackupAndRestoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerToys\\Backup");
            }

            return settingsBackupAndRestoreDir;
        }

        private List<string> GetBackupSettingsFiles(string settingsBackupAndRestoreDir)
        {
            return Directory.GetFiles(settingsBackupAndRestoreDir, "settings_*.ptb", SearchOption.TopDirectoryOnly).ToList().Where(f => Regex.IsMatch(f, "settings_(\\d{1,19}).ptb")).ToList();
        }

        /// <summary>
        /// Method <c>GetLatestSettingsFolder</c> returns a folder that has the latest backup in it.
        /// </summary>
        /// <remarks>
        /// The backup will usually be a backup file that has to be extracted to a temp folder. This will do that for us.
        /// </remarks>
        private string GetLatestSettingsFolder()
        {
            string settingsBackupAndRestoreDir = GetSettingsBackupAndRestoreDir();

            if (settingsBackupAndRestoreDir == null)
            {
                return null;
            }

            if (!Directory.Exists(settingsBackupAndRestoreDir))
            {
                return null;
            }

            var settingsBackupFolders = new Dictionary<long, string>();

            var settingsBackupFiles = GetBackupSettingsFiles(settingsBackupAndRestoreDir).ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty).Replace(".ptb", string.Empty), CultureInfo.InvariantCulture));

            var latestFolder = 0L;
            var latestFile = 0L;

            if (settingsBackupFolders.Count > 0)
            {
                latestFolder = settingsBackupFolders.OrderByDescending(x => x.Key).FirstOrDefault().Key;
            }

            if (settingsBackupFiles.Count > 0)
            {
                latestFile = settingsBackupFiles.OrderByDescending(x => x.Key).FirstOrDefault().Key;
            }

            if (latestFile == 0 && latestFolder == 0)
            {
                return null;
            }
            else if (latestFolder >= latestFile)
            {
                return settingsBackupFolders[latestFolder];
            }
            else
            {
                var tempPath = Path.GetTempPath();

                var fullBackupDir = Path.Combine(tempPath, "PowerToys_settings_" + latestFile.ToString(CultureInfo.InvariantCulture));

                lock (backupSettingsInternalLock)
                {
                    if (!Directory.Exists(fullBackupDir) || !File.Exists(Path.Combine(fullBackupDir, "manifest.json")))
                    {
                        TryDeleteDirectory(fullBackupDir);
                        ZipFile.ExtractToDirectory(settingsBackupFiles[latestFile], fullBackupDir);
                    }
                }

                ThreadPool.QueueUserWorkItem((x) =>
                {
                    try
                    {
                        RemoveOldBackups(tempPath, 1, TimeSpan.FromDays(7));
                    }
                    catch
                    {
                        // hmm, ok
                    }
                });

                return fullBackupDir;
            }
        }

        /// <summary>
        /// Method <c>GetLatestBackupFileName</c> returns the name of the newest backup file.
        /// </summary>
        public string GetLatestBackupFileName()
        {
            string settingsBackupAndRestoreDir = GetSettingsBackupAndRestoreDir();

            if (string.IsNullOrEmpty(settingsBackupAndRestoreDir) || !Directory.Exists(settingsBackupAndRestoreDir))
            {
                return string.Empty;
            }

            var settingsBackupFiles = GetBackupSettingsFiles(settingsBackupAndRestoreDir);

            if (settingsBackupFiles.Count > 0)
            {
                return Path.GetFileName(settingsBackupFiles.OrderByDescending(x => x).First());
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Method <c>GetLatestSettingsBackupManifest</c> get's the meta data from a backup file.
        /// </summary>
        public JsonNode GetLatestSettingsBackupManifest()
        {
            var folder = GetLatestSettingsFolder();
            if (folder == null)
            {
                return null;
            }

            return JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        }

        /// <summary>
        /// Method <c>IsIncludeFile</c> check's to see if a settings file is to be included during backup and restore.
        /// </summary>
        private static bool IsIncludeFile(JsonNode settings, string name)
        {
            foreach (var test in (JsonArray)settings["IncludeFiles"])
            {
                if (Regex.IsMatch(name, WildCardToRegular(test.ToString())))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Method <c>IsIgnoreFile</c> check's to see if a settings file is to be ignored during backup and restore.
        /// </summary>
        private static bool IsIgnoreFile(JsonNode settings, string name)
        {
            foreach (var test in (JsonArray)settings["IgnoreFiles"])
            {
                if (Regex.IsMatch(name, WildCardToRegular(test.ToString())))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Class <c>GetSettingsFiles</c> returns the effective list of settings files.
        /// </summary>
        /// <remarks>
        /// Handles all the included/exclude files.
        /// </remarks>
        private static string[] GetSettingsFiles(JsonNode settings, string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).Where(s => IsIncludeFile(settings, s) && !IsIgnoreFile(settings, s)).ToArray();
        }

        /// <summary>
        /// Method <c>BackupSettings</c> does the backup process.
        /// </summary>
        /// <returns>
        /// A tuple that indicates if the backup was done or not, and a message.
        /// The message usually is a localized reference key.
        /// </returns>
        /// <remarks>
        /// This is a wrapper for BackupSettingsInternal, so we can check the time to run.
        /// </remarks>
        public (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) BackupSettings(string appBasePath, string settingsBackupAndRestoreDir, bool dryRun)
        {
            var sw = Stopwatch.StartNew();
            var results = BackupSettingsInternal(appBasePath, settingsBackupAndRestoreDir, dryRun);
            sw.Stop();
            Logger.LogInfo($"BackupSettings took {sw.ElapsedMilliseconds}");
            lastBackupSettingsResults = (results.Success, results.Severity, results.LastBackupExists, DateTime.UtcNow);
            return results;
        }

        /// <summary>
        /// Method <c>DryRunBackup</c> wrapper function to do a dry-run backup
        /// </summary>
        public (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) DryRunBackup()
        {
            var settingsUtils = new SettingsUtils();
            var appBasePath = Path.GetDirectoryName(settingsUtils.GetSettingsFilePath());
            string settingsBackupAndRestoreDir = GetSettingsBackupAndRestoreDir();
            var results = BackupSettings(appBasePath, settingsBackupAndRestoreDir, true);
            lastBackupSettingsResults = (results.Success, results.Severity, results.LastBackupExists, DateTime.UtcNow);
            return results;
        }

        /// <summary>
        /// Method <c>GetLastBackupSettingsResults</c> gets the results from the last backup process
        /// </summary>
        /// <returns>
        /// A tuple that indicates if the backup was done or not, and other information
        /// </returns>
        public (bool Success, bool HadError, bool LastBackupExists, DateTime? LastRan) GetLastBackupSettingsResults()
        {
            return (lastBackupSettingsResults.Success, lastBackupSettingsResults.Severity == "Error", lastBackupSettingsResults.LastBackupExists, lastBackupSettingsResults.LastRan);
        }

        /// <summary>
        /// Method <c>BackupSettingsInternal</c> does the backup process.
        /// </summary>
        /// <returns>
        /// A tuple that indicates if the backup was done or not, and a message.
        /// The message usually is a localized reference key.
        /// </returns>
        private (bool Success, string Message, string Severity, bool LastBackupExists, string OptionalMessage) BackupSettingsInternal(string appBasePath, string settingsBackupAndRestoreDir, bool dryRun)
        {
            var lastBackupExists = false;

            lock (backupSettingsInternalLock)
            {
                // simulated delay to validate behavior
                // Thread.Sleep(1000);
                KeyValuePair<string, string> tempFile = default(KeyValuePair<string, string>);

                try
                {
                    // verify inputs
                    if (!Directory.Exists(appBasePath))
                    {
                        return (false, $"Invalid appBasePath {appBasePath}", "Error", lastBackupExists, string.Empty);
                    }

                    if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
                    {
                        return (false, $"General_SettingsBackupAndRestore_NoBackupSyncPath", "Error", lastBackupExists, "\n" + settingsBackupAndRestoreDir);
                    }

                    if (!Path.IsPathRooted(settingsBackupAndRestoreDir))
                    {
                        return (false, $"Invalid settingsBackupAndRestoreDir, not rooted", "Error", lastBackupExists, "\n" + settingsBackupAndRestoreDir);
                    }

                    if (settingsBackupAndRestoreDir.StartsWith(appBasePath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // backup cannot be under app
                        Logger.LogError($"BackupSettings, backup cannot be under app");
                        return (false, "General_SettingsBackupAndRestore_InvalidBackupLocation", "Error", lastBackupExists, "\n" + appBasePath);
                    }

                    var dirExists = TryCreateDirectory(settingsBackupAndRestoreDir);
                    if (!dirExists)
                    {
                        Logger.LogError($"Failed to create dir {settingsBackupAndRestoreDir}");
                        return (false, $"General_SettingsBackupAndRestore_BackupError", "Error", lastBackupExists, "\n" + settingsBackupAndRestoreDir);
                    }

                    // get data needed for process
                    var backupRestoreSettings = JsonNode.Parse(GetBackupRestoreSettingsJson());
                    var currentSettingsFiles = GetSettingsFiles(backupRestoreSettings, appBasePath).ToList().ToDictionary(x => x.Substring(appBasePath.Length));
                    var fullBackupDir = Path.Combine(Path.GetTempPath(), $"settings_{DateTime.UtcNow.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture)}");
                    var latestSettingsFolder = GetLatestSettingsFolder();
                    var lastBackupSettingsFiles = GetSettingsFiles(backupRestoreSettings, latestSettingsFolder).ToList().ToDictionary(x => x.Substring(latestSettingsFolder.Length));

                    lastBackupExists = lastBackupSettingsFiles.Count > 0;

                    if (currentSettingsFiles.Count == 0)
                    {
                        return (false, "General_SettingsBackupAndRestore_NoSettingsFilesFound", "Error", lastBackupExists, string.Empty);
                    }

                    var anyFileBackedUp = false;
                    var skippedSettingsFiles = new Dictionary<string, (string Path, string Settings)>();
                    var updatedSettingsFiles = new Dictionary<string, string>();

                    foreach (var currentFile in currentSettingsFiles)
                    {
                        tempFile = currentFile;

                        // need to check and back this up;
                        var currentSettingsFileToBackup = GetExportVersion(backupRestoreSettings, currentFile.Key, currentFile.Value);

                        var doBackup = false;
                        if (lastBackupSettingsFiles.TryGetValue(currentFile.Key, out string value))
                        {
                            // there is a previous backup for this, get an export version of it.
                            var lastSettingsFileDoc = GetExportVersion(backupRestoreSettings, currentFile.Key, value);

                            // check to see if the new export version would be same as last export version.
                            if (JsonNormalizer.Normalize(currentSettingsFileToBackup) != JsonNormalizer.Normalize(lastSettingsFileDoc))
                            {
                                doBackup = true;
                                Logger.LogInfo($"BackupSettings, {currentFile.Value} content is different.");
                            }
                        }
                        else
                        {
                            // this has never been backed up, we need to do it now.
                            Logger.LogInfo($"BackupSettings, {currentFile.Value} does not exists.");
                            doBackup = true;
                        }

                        if (doBackup)
                        {
                            // add to list of files we noted as needing backup
                            updatedSettingsFiles.Add(currentFile.Key, currentFile.Value);

                            // mark overall flag that a backup will be made
                            anyFileBackedUp = true;

                            // write the export version of the settings file to backup location.
                            var relativePath = currentFile.Value.Substring(appBasePath.Length + 1);
                            var backupFullPath = Path.Combine(fullBackupDir, relativePath);

                            TryCreateDirectory(fullBackupDir);
                            TryCreateDirectory(Path.GetDirectoryName(backupFullPath));

                            Logger.LogInfo($"BackupSettings writing, {backupFullPath}, dryRun:{dryRun}.");
                            if (!dryRun)
                            {
                                File.WriteAllText(backupFullPath, currentSettingsFileToBackup);
                            }
                        }
                        else
                        {
                            // if we found no reason to backup this settings file, record that in this collection
                            skippedSettingsFiles.Add(currentFile.Key, (currentFile.Value, currentSettingsFileToBackup));
                        }
                    }

                    if (!anyFileBackedUp)
                    {
                        // nothing was done!
                        return (false, $"General_SettingsBackupAndRestore_NothingToBackup", "Informational", lastBackupExists, "\n" + tempFile.Value);
                    }

                    // add skipped.
                    foreach (var currentFile in skippedSettingsFiles)
                    {
                        // if we did do a backup, we need to copy in all the settings files we skipped so the backup is complete.
                        // this is needed since we might use the backup on another machine/
                        var relativePath = currentFile.Value.Path.Substring(appBasePath.Length + 1);
                        var backupFullPath = Path.Combine(fullBackupDir, relativePath);

                        Logger.LogInfo($"BackupSettings writing, {backupFullPath}, dryRun:{dryRun}");
                        if (!dryRun)
                        {
                            TryCreateDirectory(fullBackupDir);
                            TryCreateDirectory(Path.GetDirectoryName(backupFullPath));

                            File.WriteAllText(backupFullPath, currentFile.Value.Settings);
                        }
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

                    var manifest = JsonSerializer.Serialize(manifestData, _serializerOptions);

                    if (!dryRun)
                    {
                        File.WriteAllText(Path.Combine(fullBackupDir, "manifest.json"), manifest);

                        // clean up, to prevent runaway disk usage.
                        RemoveOldBackups(settingsBackupAndRestoreDir, 10, TimeSpan.FromDays(60));

                        // compress the backup
                        var zipName = Path.Combine(settingsBackupAndRestoreDir, Path.GetFileName(fullBackupDir) + ".ptb");
                        ZipFile.CreateFromDirectory(fullBackupDir, zipName);
                        TryDeleteDirectory(fullBackupDir);
                    }

                    return (true, $"General_SettingsBackupAndRestore_BackupComplete", "Success", lastBackupExists, string.Empty);
                }
                catch (Exception ex2)
                {
                    Logger.LogError($"There was an error in {tempFile.Value} : {ex2.Message}", ex2);
                    return (false, $"General_SettingsBackupAndRestore_SettingsFormatError", "Error", lastBackupExists, "\n" + tempFile.Value);
                }
            }
        }

        /// <summary>
        /// Searches for the config file (Json) in two possible paths and returns its content.
        /// </summary>
        /// <returns>Returns the content of the config file (Json) as string.</returns>
        /// <exception cref="FileNotFoundException">Thrown if file is not found.</exception>
        /// <remarks>If the settings window is launched from an installed instance of PT we need the path "...\Settings\\backup_restore_settings.json" and if the settings window is launched from a local VS build of PT we need the path "...\backup_restore_settings.json".</remarks>
        private static string GetBackupRestoreSettingsJson()
        {
            if (File.Exists("backup_restore_settings.json"))
            {
                return File.ReadAllText("backup_restore_settings.json");
            }
            else if (File.Exists("Settings\\backup_restore_settings.json"))
            {
                return File.ReadAllText("Settings\\backup_restore_settings.json");
            }
            else
            {
                throw new FileNotFoundException($"The backup_restore_settings.json could not be found at {Environment.CurrentDirectory}");
            }
        }

        /// <summary>
        /// Method <c>WildCardToRegular</c> is so we can use 'normal' wildcard syntax and instead of regex
        /// </summary>
        private static string WildCardToRegular(string value)
        {
            return "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
        }

        /// <summary>
        /// Method <c>GetExportVersion</c> gets the version of the settings file that we want to backup.
        /// It will be formatted and all problematic settings removed from it.
        /// </summary>
        public static string GetExportVersion(JsonNode backupRestoreSettings, string settingFileKey, string settingsFileName)
        {
            var ignoredSettings = GetIgnoredSettings(backupRestoreSettings, settingFileKey);
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
                var ptRunIgnoredSettings = GetPTRunIgnoredSettings(backupRestoreSettings);
                var ptrSettings = JsonNode.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));

                foreach (JsonObject pluginToChange in ptRunIgnoredSettings)
                {
                    foreach (JsonObject plugin in (JsonArray)ptrSettings["plugins"])
                    {
                        if (plugin["Id"].ToString() == pluginToChange["Id"].ToString())
                        {
                            foreach (var nameOfPropertyToRemove in (JsonArray)pluginToChange["Names"])
                            {
                                plugin.Remove(nameOfPropertyToRemove.ToString());
                            }
                        }
                    }
                }

                return ptrSettings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return JsonNode.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan)).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Method <c>GetPTRunIgnoredSettings</c> gets the 'Run-Plugin-level' settings we should ignore because they are problematic to backup/restore.
        /// </summary>
        private static JsonArray GetPTRunIgnoredSettings(JsonNode backupRestoreSettings)
        {
            ArgumentNullException.ThrowIfNull(backupRestoreSettings);

            if (backupRestoreSettings["IgnoredPTRunSettings"] != null)
            {
                return (JsonArray)backupRestoreSettings["IgnoredPTRunSettings"];
            }

            return new JsonArray();
        }

        /// <summary>
        /// Method <c>GetIgnoredSettings</c> gets the 'top-level' settings we should ignore because they are problematic to backup/restore.
        /// </summary>
        private static string[] GetIgnoredSettings(JsonNode backupRestoreSettings, string settingFileKey)
        {
            ArgumentNullException.ThrowIfNull(backupRestoreSettings);

            if (settingFileKey.StartsWith("\\", StringComparison.OrdinalIgnoreCase))
            {
                settingFileKey = settingFileKey.Substring(1);
            }

            if (backupRestoreSettings["IgnoredSettings"] != null)
            {
                if (backupRestoreSettings["IgnoredSettings"][settingFileKey] != null)
                {
                    var settingsArray = (JsonArray)backupRestoreSettings["IgnoredSettings"][settingFileKey];

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

        /// <summary>
        /// Method <c>RemoveOldBackups</c> is a helper that prevents is from having some runaway disk usages.
        /// </summary>
        private static void RemoveOldBackups(string location, int minNumberToKeep, TimeSpan deleteIfOlderThanTs)
        {
            if (!Monitor.TryEnter(removeOldBackupsLock, 1000))
            {
                return;
            }

            try
            {
                DateTime deleteIfOlder = DateTime.UtcNow.Subtract(deleteIfOlderThanTs);

                var settingsBackupFolders = Directory.GetDirectories(location, "settings_*", SearchOption.TopDirectoryOnly).ToList().Where(f => Regex.IsMatch(f, "settings_(\\d{1,19})")).ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty), CultureInfo.InvariantCulture)).ToList();

                settingsBackupFolders.AddRange(Directory.GetDirectories(location, "PowerToys_settings_*", SearchOption.TopDirectoryOnly).ToList().Where(f => Regex.IsMatch(f, "PowerToys_settings_(\\d{1,19})")).ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("PowerToys_settings_", string.Empty), CultureInfo.InvariantCulture)));

                var settingsBackupFiles = Directory.GetFiles(location, "settings_*.ptb", SearchOption.TopDirectoryOnly).ToList().Where(f => Regex.IsMatch(f, "settings_(\\d{1,19}).ptb")).ToDictionary(x => long.Parse(Path.GetFileName(x).Replace("settings_", string.Empty).Replace(".ptb", string.Empty), CultureInfo.InvariantCulture));

                if (settingsBackupFolders.Count + settingsBackupFiles.Count <= minNumberToKeep)
                {
                    return;
                }

                foreach (var item in settingsBackupFolders)
                {
                    var backupTime = DateTime.FromFileTimeUtc(item.Key);

                    if (item.Value.Contains("PowerToys_settings_", StringComparison.OrdinalIgnoreCase))
                    {
                        // this is a temp backup and we want to clean based on the time it was created in the temp place, not the time the backup was made.
                        var folderCreatedTime = new DirectoryInfo(item.Value).CreationTimeUtc;

                        if (folderCreatedTime > backupTime)
                        {
                            backupTime = folderCreatedTime;
                        }
                    }

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

                foreach (var item in settingsBackupFiles)
                {
                    var backupTime = DateTime.FromFileTimeUtc(item.Key);

                    if (backupTime < deleteIfOlder)
                    {
                        try
                        {
                            Logger.LogInfo($"RemoveOldBackups killing {item.Value}");
                            File.Delete(item.Value);
                        }
                        catch (Exception ex2)
                        {
                            Logger.LogError($"Failed to remove a setting backup folder ({item.Value}), because: ({ex2.Message})");
                        }
                    }
                }
            }
            finally
            {
                Monitor.Exit(removeOldBackupsLock);
            }
        }

        /// <summary>
        /// Class <c>JsonNormalizer</c> is a utility class to 'normalize' a JSON file so that it can be compared to another JSON file.
        /// This really just means to fully sort it. This does not work for any JSON file where the order of the node is relevant.
        /// </summary>
        private sealed class JsonNormalizer
        {
            public static string Normalize(string json)
            {
                var doc1 = JsonNormalizer.Deserialize(json);
                var newJson1 = JsonSerializer.Serialize(doc1, _serializerOptions);
                return newJson1;
            }

            private static List<object> DeserializeArray(string json)
            {
                var result = JsonSerializer.Deserialize<List<object>>(json);

                var updates = new List<object>();

                foreach (var item in result)
                {
                    if (item != null)
                    {
                        var currentItem = (JsonElement)item;

                        if (currentItem.ValueKind == JsonValueKind.Object)
                        {
                            updates.Add(Deserialize(currentItem.ToString()));
                        }
                        else if (((JsonElement)item).ValueKind == JsonValueKind.Array)
                        {
                            updates.Add(DeserializeArray(currentItem.ToString()));
                        }
                        else
                        {
                            updates.Add(item);
                        }
                    }
                    else
                    {
                        updates.Add(item);
                    }
                }

                return updates.OrderBy(x => JsonSerializer.Serialize(x)).ToList();
            }

            private static Dictionary<string, object> Deserialize(string json)
            {
                var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                var updates = new Dictionary<string, object>();

                foreach (var item in doc)
                {
                    if (item.Value != null)
                    {
                        if (((JsonElement)item.Value).ValueKind == JsonValueKind.Object)
                        {
                            updates.Add(item.Key, Deserialize(((JsonElement)item.Value).ToString()));
                        }
                        else if (((JsonElement)item.Value).ValueKind == JsonValueKind.Array)
                        {
                            updates.Add(item.Key, DeserializeArray(((JsonElement)item.Value).ToString()));
                        }
                    }
                }

                foreach (var item in updates)
                {
                    doc.Remove(item.Key);
                    doc.Add(item.Key, item.Value);
                }

                var ordered = new Dictionary<string, object>();

                foreach (var item in doc.Keys.OrderBy(x => x))
                {
                    ordered.Add(item, doc[item]);
                }

                return ordered;
            }
        }
    }
}
