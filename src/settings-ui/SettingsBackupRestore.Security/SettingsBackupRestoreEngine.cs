// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Production backup and restore orchestration built on validated handle-relative I/O.
/// </summary>
public sealed class SettingsBackupRestoreEngine
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new() { WriteIndented = true };
    private readonly BackupRestorePolicy policy;

    /// <summary>
    /// Initializes a new instance from the existing backup_restore_settings.json contract.
    /// </summary>
    public SettingsBackupRestoreEngine(string policyJson)
    {
        policy = BackupRestorePolicy.Parse(policyJson);
    }

    /// <summary>
    /// Compares settings with the newest archive and optionally writes a compatible .ptb archive.
    /// </summary>
    public BackupOperationResult Backup(
        string settingsRootPath,
        string backupRootPath,
        string stagingParentPath,
        bool dryRun,
        string productVersion,
        string machineName,
        DateTime utcNow)
    {
        using SecureDirectoryRoot settingsRoot = SecureDirectoryRoot.OpenReadOnly(settingsRootPath);
        IReadOnlyDictionary<string, string> current = ReadSettings(settingsRoot);
        if (current.Count == 0)
        {
            throw new InvalidDataException("No settings files were found.");
        }

        SecureDirectoryRoot? backupRoot = null;
        SecureDirectoryRoot? stagingParent = null;
        SecureDirectoryRoot? previousStaging = null;
        try
        {
            if (Directory.Exists(backupRootPath))
            {
                backupRoot = dryRun ? SecureDirectoryRoot.OpenReadOnly(backupRootPath) : SecureDirectoryRoot.Open(backupRootPath);
            }
            else if (!dryRun)
            {
                backupRoot = SecureDirectoryRoot.OpenOrCreate(backupRootPath);
            }

            if (backupRoot != null && SecurePath.IsContained(settingsRoot.FinalPath, backupRoot.FinalPath))
            {
                throw new IOException("The backup root cannot be inside the settings root.");
            }

            string? latestArchive = backupRoot == null ? null : GetLatestArchiveFileName(backupRoot);
            IReadOnlyDictionary<string, string> previous = new Dictionary<string, string>(WindowsPathComparer.Instance);
            if (latestArchive != null)
            {
                stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
                previousStaging = BackupRestoreArchive.ExtractToExclusiveStaging(backupRoot!, latestArchive, stagingParent);
                previous = ReadSettings(previousStaging);
            }

            List<string> updated = current
                .Where(item => !previous.TryGetValue(item.Key, out string? value) || !JsonEquivalent(item.Value, value))
                .Select(item => item.Key)
                .ToList();
            if (updated.Count == 0)
            {
                return new BackupOperationResult(false, latestArchive != null, latestArchive);
            }

            if (dryRun)
            {
                return new BackupOperationResult(true, latestArchive != null, latestArchive);
            }

            string archiveName = CreateArchive(
                backupRoot!,
                current,
                updated,
                productVersion,
                machineName,
                utcNow);
            RemoveOldArchives(backupRoot!, utcNow.Subtract(TimeSpan.FromDays(60)), 10);
            return new BackupOperationResult(true, latestArchive != null, archiveName);
        }
        finally
        {
            CleanupStaging(previousStaging);
            stagingParent?.Dispose();
            backupRoot?.Dispose();
        }
    }

    /// <summary>
    /// Builds the restore confirmation model from the newest validated archive.
    /// </summary>
    public RestorePreviewViewModel CreateRestorePreview(string settingsRootPath, string backupRootPath, string stagingParentPath)
    {
        using SecureDirectoryRoot settingsRoot = SecureDirectoryRoot.OpenReadOnly(settingsRootPath);
        using SecureDirectoryRoot backupRoot = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        using SecureDirectoryRoot stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
        string archiveName = GetLatestArchiveFileName(backupRoot) ?? throw new FileNotFoundException("No settings backup was found.");
        SecureDirectoryRoot? staging = null;
        try
        {
            using SecureFile archiveFile = backupRoot.OpenFileForRead(archiveName);
            string archiveSha256 = ComputeSha256(archiveFile);
            staging = BackupRestoreArchive.ExtractToExclusiveStaging(archiveFile, stagingParent);
            return RestorePreviewViewModel.Create(
                    policy,
                    staging.EnumerateFiles(fileFilter: IsJsonOrManifest),
                    settingsRoot.EnumerateFiles(fileFilter: IsIncludedJson))
                .WithArchiveIdentity(archiveName, archiveSha256);
        }
        finally
        {
            CleanupStaging(staging);
        }
    }

    /// <summary>
    /// Restores included settings from the newest validated archive.
    /// </summary>
    public RestoreOperationResult Restore(
        string settingsRootPath,
        string backupRootPath,
        string stagingParentPath,
        string? archiveFileName = null,
        string? expectedArchiveSha256 = null)
    {
        using SecureDirectoryRoot settingsRoot = SecureDirectoryRoot.Open(settingsRootPath);
        using SecureDirectoryRoot backupRoot = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        using SecureDirectoryRoot stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
        string archiveName = archiveFileName ?? GetLatestArchiveFileName(backupRoot) ?? throw new FileNotFoundException("No settings backup was found.");
        SecureDirectoryRoot? staging = null;
        try
        {
            using SecureFile archiveFile = backupRoot.OpenFileForRead(archiveName);
            if (expectedArchiveSha256 != null &&
                !string.Equals(ComputeSha256(archiveFile), expectedArchiveSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The selected settings backup changed after preview.");
            }

            staging = BackupRestoreArchive.ExtractToExclusiveStaging(archiveFile, stagingParent);
            HashSet<string> currentPaths = new(settingsRoot.EnumerateFiles(fileFilter: IsIncludedJson), WindowsPathComparer.Instance);
            List<RestoreWriteOperation> operations = [];
            try
            {
                foreach (string relativePath in staging.EnumerateFiles(fileFilter: IsJsonOrManifest))
                {
                    if (relativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || !policy.ShouldInclude(relativePath))
                    {
                        continue;
                    }

                    using SecureFile backupFile = staging.OpenFileForRead(relativePath);
                    string restoreJson = policy.CreateExportVersion(relativePath, backupFile.ReadAllText());
                    if (currentPaths.Contains(relativePath))
                    {
                        SecureFile currentFile = settingsRoot.OpenFileForOverwrite(relativePath);
                        try
                        {
                            string originalJson = currentFile.ReadAllText();
                            string currentJson = policy.CreateExportVersion(relativePath, originalJson);
                            if (JsonEquivalent(currentJson, restoreJson))
                            {
                                currentFile.Dispose();
                                continue;
                            }

                            string contents = policy.GetRestoreMode(relativePath) == RestoreMode.Overwrite
                                ? restoreJson
                                : JsonSettingsMerge.Merge(originalJson, restoreJson);
                            operations.Add(new RestoreWriteOperation(relativePath, contents, originalJson, currentFile, isNew: false));
                        }
                        catch
                        {
                            currentFile.Dispose();
                            throw;
                        }
                    }
                    else
                    {
                        operations.Add(new RestoreWriteOperation(relativePath, restoreJson, originalContents: null, targetFile: null, isNew: true));
                    }
                }
            }
            catch
            {
                foreach (RestoreWriteOperation operation in operations)
                {
                    operation.Dispose();
                }

                throw;
            }

            if (operations.Count == 0)
            {
                return new RestoreOperationResult(false, false);
            }

            ApplyRestoreOperations(settingsRoot, operations);
            return new RestoreOperationResult(true, policy.RestartAfterRestore);
        }
        finally
        {
            CleanupStaging(staging);
        }
    }

    /// <summary>
    /// Reads the newest archive manifest through validated extraction handles.
    /// </summary>
    public JsonNode? GetLatestManifest(string backupRootPath, string stagingParentPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return null;
        }

        using SecureDirectoryRoot backupRoot = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        using SecureDirectoryRoot stagingParent = SecureDirectoryRoot.Open(stagingParentPath);
        string? archiveName = GetLatestArchiveFileName(backupRoot);
        if (archiveName == null)
        {
            return null;
        }

        SecureDirectoryRoot? staging = null;
        try
        {
            staging = BackupRestoreArchive.ExtractToExclusiveStaging(backupRoot, archiveName, stagingParent);
            using SecureFile manifest = staging.OpenFileForRead("manifest.json");
            return JsonNode.Parse(manifest.ReadAllText());
        }
        finally
        {
            CleanupStaging(staging);
        }
    }

    /// <summary>
    /// Gets the newest compatible archive name without opening untrusted child paths by name.
    /// </summary>
    public static string? GetLatestArchiveFileName(string backupRootPath)
    {
        if (!Directory.Exists(backupRootPath))
        {
            return null;
        }

        using SecureDirectoryRoot root = SecureDirectoryRoot.OpenReadOnly(backupRootPath);
        return GetLatestArchiveFileName(root);
    }

    private static string? GetLatestArchiveFileName(SecureDirectoryRoot backupRoot)
    {
        return backupRoot.EnumerateFiles(recursive: false, fileFilter: IsArchiveCandidate)
            .Select(path => (Path: path, Timestamp: ParseArchiveTimestamp(path)))
            .Where(item => item.Timestamp.HasValue && Path.GetDirectoryName(item.Path) == string.Empty)
            .OrderByDescending(item => item.Timestamp)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private IReadOnlyDictionary<string, string> ReadSettings(SecureDirectoryRoot root)
    {
        Dictionary<string, string> settings = new(WindowsPathComparer.Instance);
        foreach (string relativePath in root.EnumerateFiles(fileFilter: IsIncludedJson))
        {
            using SecureFile file = root.OpenFileForRead(relativePath);
            settings.Add(relativePath, policy.CreateExportVersion(relativePath, file.ReadAllText()));
        }

        return settings;
    }

    private static string CreateArchive(
        SecureDirectoryRoot backupRoot,
        IReadOnlyDictionary<string, string> settings,
        IReadOnlyCollection<string> updated,
        string productVersion,
        string machineName,
        DateTime utcNow)
    {
        long timestamp = utcNow.ToFileTimeUtc();
        for (int attempt = 0; attempt < 32; attempt++)
        {
            string archiveName = $"settings_{timestamp + attempt}.ptb";
            SecureFile? archiveFile = null;
            try
            {
                archiveFile = backupRoot.CreateNewFile(archiveName);
                using (ZipArchive archive = new(archiveFile.Stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach ((string path, string contents) in settings.OrderBy(item => item.Key, WindowsPathComparer.Instance))
                    {
                        WriteEntry(archive, path, contents);
                    }

                    string manifest = JsonSerializer.Serialize(
                        new
                        {
                            CreateDateTime = utcNow.ToString("u", CultureInfo.InvariantCulture),
                            Version = productVersion,
                            UpdatedFiles = updated.Select(AddLegacyManifestPrefix).ToList(),
                            BackupSource = machineName,
                            UnchangedFiles = settings.Keys
                                .Except(updated, WindowsPathComparer.Instance)
                                .Select(AddLegacyManifestPrefix)
                                .ToList(),
                        },
                        ManifestSerializerOptions);
                    WriteEntry(archive, "manifest.json", manifest);
                }

                archiveFile.Dispose();
                return archiveName;
            }
            catch (IOException) when (archiveFile == null)
            {
                continue;
            }
            catch
            {
                archiveFile?.Dispose();
                try
                {
                    backupRoot.DeleteEntry(archiveName, isDirectory: false);
                }
                catch
                {
                }

                throw;
            }
        }

        throw new IOException("Could not create a unique settings backup archive.");
    }

    private static void WriteEntry(ZipArchive archive, string path, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open());
        writer.Write(contents);
    }

    private static void RemoveOldArchives(SecureDirectoryRoot backupRoot, DateTime deleteBefore, int minimumToKeep)
    {
        List<(string Path, long Timestamp)> archives = backupRoot.EnumerateFiles(recursive: false, fileFilter: IsArchiveCandidate)
            .Select(path => (Path: path, Timestamp: ParseArchiveTimestamp(path)))
            .Where(item => item.Timestamp.HasValue && Path.GetDirectoryName(item.Path) == string.Empty)
            .Select(item => (item.Path, item.Timestamp!.Value))
            .OrderByDescending(item => item.Value)
            .ToList();
        foreach ((string path, long timestamp) in archives.Skip(minimumToKeep))
        {
            if (DateTime.FromFileTimeUtc(timestamp) < deleteBefore)
            {
                try
                {
                    backupRoot.DeleteEntry(path, isDirectory: false);
                }
                catch
                {
                }
            }
        }
    }

    private static string AddLegacyManifestPrefix(string path)
    {
        return path.StartsWith('\\') ? path : "\\" + path;
    }

    private bool IsIncludedJson(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && policy.ShouldInclude(path);
    }

    private static bool IsJsonOrManifest(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchiveCandidate(string path)
    {
        return ParseArchiveTimestamp(path).HasValue;
    }

    private static long? ParseArchiveTimestamp(string path)
    {
        string fileName = Path.GetFileName(path);
        if (!fileName.StartsWith("settings_", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".ptb", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string value = fileName["settings_".Length..^".ptb".Length];
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp) ? timestamp : null;
    }

    private static bool JsonEquivalent(string left, string right)
    {
        return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
    }

    private static string ComputeSha256(SecureFile archiveFile)
    {
        archiveFile.Stream.Position = 0;
        string hash = Convert.ToHexString(SHA256.HashData(archiveFile.Stream));
        archiveFile.Stream.Position = 0;
        return hash;
    }

    private static void CleanupStaging(SecureDirectoryRoot? staging)
    {
        if (staging == null)
        {
            return;
        }

        try
        {
            try
            {
                staging.DeleteTree();
            }
            catch
            {
            }
        }
        finally
        {
            staging.Dispose();
        }
    }

    private static void ApplyRestoreOperations(SecureDirectoryRoot settingsRoot, List<RestoreWriteOperation> operations)
    {
        List<string> createdFiles = [];
        SortedSet<string> createdDirectories = new(WindowsPathComparer.Instance);
        List<RestoreWriteOperation> overwritten = [];
        try
        {
            foreach (RestoreWriteOperation operation in operations.Where(operation => operation.IsNew))
            {
                foreach (string directory in GetParentDirectories(operation.RelativePath))
                {
                    if (!settingsRoot.DirectoryExists(directory))
                    {
                        createdDirectories.Add(directory);
                    }
                }

                operation.TargetFile = settingsRoot.CreateNewFile(operation.RelativePath);
                createdFiles.Add(operation.RelativePath);
            }

            foreach (RestoreWriteOperation operation in operations)
            {
                if (!operation.IsNew)
                {
                    overwritten.Add(operation);
                }

                operation.TargetFile!.OverwriteAllText(operation.NewContents);
            }
        }
        catch (Exception restoreException)
        {
            List<Exception> rollbackErrors = [];
            for (int index = overwritten.Count - 1; index >= 0; index--)
            {
                TryRollback(
                    () => overwritten[index].TargetFile!.OverwriteAllText(overwritten[index].OriginalContents!),
                    rollbackErrors);
            }

            foreach (RestoreWriteOperation operation in operations.Where(operation => operation.IsNew))
            {
                operation.TargetFile?.Dispose();
                operation.TargetFile = null;
            }

            for (int index = createdFiles.Count - 1; index >= 0; index--)
            {
                string path = createdFiles[index];
                TryRollback(() => settingsRoot.DeleteEntry(path, isDirectory: false), rollbackErrors);
            }

            foreach (string directory in createdDirectories.OrderByDescending(path => path.Length))
            {
                TryRollback(() => settingsRoot.DeleteEntry(directory, isDirectory: true), rollbackErrors);
            }

            if (rollbackErrors.Count > 0)
            {
                restoreException.Data["RestoreRollbackErrors"] = rollbackErrors.ToArray();
            }

            throw;
        }
        finally
        {
            foreach (RestoreWriteOperation operation in operations)
            {
                operation.Dispose();
            }
        }
    }

    private static IEnumerable<string> GetParentDirectories(string relativePath)
    {
        int separatorIndex = relativePath.IndexOf('\\');
        while (separatorIndex >= 0)
        {
            yield return relativePath[..separatorIndex];
            separatorIndex = relativePath.IndexOf('\\', separatorIndex + 1);
        }
    }

    private static void TryRollback(Action rollback, List<Exception> rollbackErrors)
    {
        try
        {
            rollback();
        }
        catch (Exception exception)
        {
            rollbackErrors.Add(exception);
        }
    }

    private sealed class RestoreWriteOperation : IDisposable
    {
        internal RestoreWriteOperation(
            string relativePath,
            string newContents,
            string? originalContents,
            SecureFile? targetFile,
            bool isNew)
        {
            RelativePath = relativePath;
            NewContents = newContents;
            OriginalContents = originalContents;
            TargetFile = targetFile;
            IsNew = isNew;
        }

        internal string RelativePath { get; }

        internal string NewContents { get; }

        internal string? OriginalContents { get; }

        internal SecureFile? TargetFile { get; set; }

        internal bool IsNew { get; }

        public void Dispose()
        {
            TargetFile?.Dispose();
            TargetFile = null;
        }
    }
}
