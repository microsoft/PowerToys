// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Microsoft.Data.Sqlite;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspacesApi
    {
        // Periodically refresh even if the storage files are unchanged, e.g. to drop folders that were deleted.
        private static readonly TimeSpan MaxCacheAge = TimeSpan.FromMinutes(1);

        private readonly Lock _cacheLock = new();
        private readonly Lock _refreshLock = new();

        private List<VSCodeWorkspace> _cachedWorkspaces;
        private string _cachedSignature;
        private DateTime _cachedAtUtc;
        private bool _isBackgroundRefreshRunning;

        public VSCodeWorkspacesApi()
        {
        }

        private VSCodeWorkspace ParseVSCodeUriAndAuthority(string uri, string authority, VSCodeInstance vscodeInstance, bool isWorkspace = false)
        {
            if (uri is null)
            {
                return null;
            }

            var rfc3986Uri = Rfc3986Uri.Parse(Uri.UnescapeDataString(uri));
            if (rfc3986Uri is null)
            {
                return null;
            }

            var isFileUri = string.Equals(
                rfc3986Uri.Scheme,
                Uri.UriSchemeFile,
                StringComparison.OrdinalIgnoreCase);

            var isUncFileUri =
                isFileUri &&
                string.IsNullOrEmpty(authority) &&
                !string.IsNullOrEmpty(rfc3986Uri.Authority) &&
                !string.Equals(
                    rfc3986Uri.Authority,
                    "localhost",
                    StringComparison.OrdinalIgnoreCase);

            // file://server/share is a local Windows UNC path, not a VS Code remote URI.
            var effectiveAuthority =
                isFileUri && string.IsNullOrEmpty(authority)
                    ? string.Empty
                    : authority ?? rfc3986Uri.Authority;

            var (workspaceEnv, machineName) =
                ParseVSCodeAuthority.GetWorkspaceEnvironment(effectiveAuthority);

            if (workspaceEnv is null)
            {
                return null;
            }

            var path = isUncFileUri
                ? $@"\\{rfc3986Uri.Authority}{rfc3986Uri.Path.Replace('/', '\\')}"
                : rfc3986Uri.Path;

            // file:///C:/... becomes C:/...
            if (workspaceEnv == WorkspaceEnvironment.Local && !isUncFileUri)
            {
                path = path[1..];
            }

            if (!DoesPathExist(path, workspaceEnv.Value))
            {
                return null;
            }

            var folderName = Path.GetFileName(path);

            // Check we haven't returned '' if we have a path like C:\
            if (string.IsNullOrEmpty(folderName))
            {
                DirectoryInfo dirInfo = new(path);
                folderName = dirInfo.Name.TrimEnd(':');
            }

            return new VSCodeWorkspace()
            {
                Path = uri,
                WorkspaceType = isWorkspace ? WorkspaceType.WorkspaceFile : WorkspaceType.ProjectFolder,
                RelativePath = path,
                FolderName = folderName,
                ExtraInfo = machineName,
                WorkspaceEnvironment = workspaceEnv ?? default,
                VSCodeInstance = vscodeInstance,
            };
        }

        private bool DoesPathExist(string path, WorkspaceEnvironment workspaceEnv)
        {
            if (workspaceEnv == WorkspaceEnvironment.Local)
            {
                return Directory.Exists(path) || File.Exists(path);
            }

            // If the workspace environment is not Local or WSL, assume the path exists
            return true;
        }

        /// <summary>
        /// Gets the recently opened workspaces of all VS Code instances.
        /// </summary>
        /// <remarks>
        /// Reading and parsing the VS Code storage files can be slow, and this property is read on every query.
        /// The first read loads the workspaces synchronously. Later reads return the cached workspaces immediately
        /// and refresh them in the background when the storage files changed or the cache is older than <see cref="MaxCacheAge"/>.
        /// </remarks>
        public List<VSCodeWorkspace> Workspaces
        {
            get
            {
                var currentSignature = GetStorageFilesSignature();
                List<VSCodeWorkspace> cachedWorkspaces;

                lock (_cacheLock)
                {
                    cachedWorkspaces = _cachedWorkspaces;

                    if (cachedWorkspaces != null
                        && !_isBackgroundRefreshRunning
                        && (currentSignature != _cachedSignature || DateTime.UtcNow - _cachedAtUtc > MaxCacheAge))
                    {
                        _isBackgroundRefreshRunning = true;
                        Task.Run(RefreshInBackground);
                    }
                }

                cachedWorkspaces ??= LoadIfNotCached();

                // Return a copy so callers can't modify the cached list.
                return new List<VSCodeWorkspace>(cachedWorkspaces);
            }
        }

        private List<VSCodeWorkspace> LoadIfNotCached()
        {
            lock (_refreshLock)
            {
                lock (_cacheLock)
                {
                    if (_cachedWorkspaces != null)
                    {
                        return _cachedWorkspaces;
                    }
                }

                return Refresh();
            }
        }

        private void RefreshInBackground()
        {
            try
            {
                lock (_refreshLock)
                {
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                Log.Exception("Failed to refresh VS Code workspaces", ex, GetType());
            }
            finally
            {
                lock (_cacheLock)
                {
                    _isBackgroundRefreshRunning = false;
                }
            }
        }

        // Must be called while holding _refreshLock.
        private List<VSCodeWorkspace> Refresh()
        {
            // Take the signature before loading, so changes made while loading trigger another refresh.
            var signature = GetStorageFilesSignature();
            var workspaces = LoadWorkspaces();

            lock (_cacheLock)
            {
                _cachedWorkspaces = workspaces;
                _cachedSignature = signature;
                _cachedAtUtc = DateTime.UtcNow;
            }

            return workspaces;
        }

        private static string GetStorageFilesSignature()
        {
            var signature = new StringBuilder();

            foreach (var vscodeInstance in VSCodeInstances.Instances)
            {
                foreach (var storageFilePath in GetStorageFilePaths(vscodeInstance))
                {
                    var fileInfo = new FileInfo(storageFilePath);
                    signature.Append(storageFilePath).Append('|');

                    if (fileInfo.Exists)
                    {
                        signature.Append(fileInfo.LastWriteTimeUtc.Ticks).Append('|').Append(fileInfo.Length);
                    }

                    signature.Append(';');
                }
            }

            return signature.ToString();
        }

        private static IEnumerable<string> GetStorageFilePaths(VSCodeInstance vscodeInstance)
        {
            yield return GetStorageJsonPath(vscodeInstance);

            foreach (var storageDbPath in GetStorageDbPaths(vscodeInstance))
            {
                yield return storageDbPath;

                // SQLite may write changes to the write-ahead log without touching the database file.
                yield return storageDbPath + "-wal";
            }
        }

        // storage.json contains opened Workspaces
        private static string GetStorageJsonPath(VSCodeInstance vscodeInstance) => Path.Combine(vscodeInstance.AppData, "storage.json");

        private static IEnumerable<string> GetStorageDbPaths(VSCodeInstance vscodeInstance)
        {
            // User/globalStorage/state.vscdb - history.recentlyOpenedPathsList - vscode v1.64 or later
            var vscode_storage_db = Path.Combine(vscodeInstance.AppData, "User/globalStorage/state.vscdb");
            var vscode_shared_storage_db = vscodeInstance.SharedStorageDbPath;

            return new[] { vscode_storage_db, vscode_shared_storage_db }
                .Where(filePath => !string.IsNullOrEmpty(filePath))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private List<VSCodeWorkspace> LoadWorkspaces()
        {
            var results = new List<VSCodeWorkspace>();

            foreach (var vscodeInstance in VSCodeInstances.Instances)
            {
                var vscode_storage = GetStorageJsonPath(vscodeInstance);

                if (File.Exists(vscode_storage))
                {
                    var storageResults = GetWorkspacesInJson(vscodeInstance, vscode_storage);
                    results.AddRange(storageResults);
                }

                foreach (var storageDbPath in GetStorageDbPaths(vscodeInstance))
                {
                    if (File.Exists(storageDbPath))
                    {
                        var storageDbResults = GetWorkspacesInVscdb(vscodeInstance, storageDbPath);
                        results.AddRange(storageDbResults);
                    }
                }
            }

            return results
                .Where(workspace => workspace != null)
                .GroupBy(GetWorkspaceKey, StringComparer.OrdinalIgnoreCase)
                .Select(workspaceGroup => workspaceGroup.First())
                .ToList();
        }

        private static string GetWorkspaceKey(VSCodeWorkspace workspace)
        {
            return string.Join(
                "|",
                workspace.VSCodeInstance?.ExecutablePath ?? string.Empty,
                workspace.WorkspaceType,
                workspace.Path ?? string.Empty);
        }

        private List<VSCodeWorkspace> GetWorkspacesInJson(VSCodeInstance vscodeInstance, string filePath)
        {
            var storageFileResults = new List<VSCodeWorkspace>();

            var fileContent = File.ReadAllText(filePath);

            try
            {
                VSCodeStorageFile vscodeStorageFile = JsonSerializer.Deserialize<VSCodeStorageFile>(fileContent);

                if (vscodeStorageFile != null && vscodeStorageFile.OpenedPathsList != null)
                {
                    // for previous versions of vscode
                    if (vscodeStorageFile.OpenedPathsList.Workspaces3 != null)
                    {
                        foreach (var workspaceUri in vscodeStorageFile.OpenedPathsList.Workspaces3)
                        {
                            var workspace = ParseVSCodeUriAndAuthority(workspaceUri, null, vscodeInstance);
                            if (workspace != null)
                            {
                                storageFileResults.Add(workspace);
                            }
                        }
                    }

                    // vscode v1.55.0 or later
                    if (vscodeStorageFile.OpenedPathsList.Entries != null)
                    {
                        foreach (var entry in vscodeStorageFile.OpenedPathsList.Entries)
                        {
                            bool isWorkspaceFile = false;
                            var uri = entry.FolderUri;
                            if (entry.Workspace != null && entry.Workspace.ConfigPath != null)
                            {
                                isWorkspaceFile = true;
                                uri = entry.Workspace.ConfigPath;
                            }

                            var workspace = ParseVSCodeUriAndAuthority(uri, entry.RemoteAuthority, vscodeInstance, isWorkspaceFile);
                            if (workspace != null)
                            {
                                storageFileResults.Add(workspace);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var message = $"Failed to deserialize {filePath}";
                Log.Exception(message, ex, GetType());
            }

            return storageFileResults;
        }

        private List<VSCodeWorkspace> GetWorkspacesInVscdb(VSCodeInstance vscodeInstance, string filePath)
        {
            var dbFileResults = new List<VSCodeWorkspace>();
            SqliteConnection sqliteConnection = null;
            try
            {
                sqliteConnection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly;");
                sqliteConnection.Open();

                if (sqliteConnection.State == System.Data.ConnectionState.Open)
                {
                    var sqlite_cmd = sqliteConnection.CreateCommand();
                    sqlite_cmd.CommandText = "SELECT value FROM ItemTable WHERE key LIKE 'history.recentlyOpenedPathsList'";

                    var sqlite_datareader = sqlite_cmd.ExecuteReader();

                    if (sqlite_datareader.Read())
                    {
                        string entries = sqlite_datareader.GetString(0);
                        if (!string.IsNullOrEmpty(entries))
                        {
                            VSCodeStorageEntries vscodeStorageEntries = JsonSerializer.Deserialize<VSCodeStorageEntries>(entries);
                            if (vscodeStorageEntries.Entries != null)
                            {
                                vscodeStorageEntries.Entries = vscodeStorageEntries.Entries.Where(x => x != null).ToList();
                                foreach (var entry in vscodeStorageEntries.Entries)
                                {
                                    bool isWorkspaceFile = false;
                                    var uri = entry.FolderUri;
                                    if (entry.Workspace != null && entry.Workspace.ConfigPath != null)
                                    {
                                        isWorkspaceFile = true;
                                        uri = entry.Workspace.ConfigPath;
                                    }

                                    var workspace = ParseVSCodeUriAndAuthority(uri, entry.RemoteAuthority, vscodeInstance, isWorkspaceFile);
                                    if (workspace != null)
                                    {
                                        dbFileResults.Add(workspace);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                var message = $"Failed to retrieve workspaces from db: {filePath}";
                Log.Exception(message, e, GetType());
            }
            finally
            {
                sqliteConnection?.Close();
            }

            return dbFileResults;
        }
    }
}
