// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Microsoft.Data.Sqlite;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspacesApi
    {
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

            var (workspaceEnv, machineName) = ParseVSCodeAuthority.GetWorkspaceEnvironment(authority ?? rfc3986Uri.Authority);
            if (workspaceEnv is null)
            {
                return null;
            }

            var path = rfc3986Uri.Path;

            // Remove preceding '/' from local (Windows) path
            if (workspaceEnv == WorkspaceEnvironment.Local)
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

        public List<VSCodeWorkspace> Workspaces
        {
            get
            {
                var results = new List<VSCodeWorkspace>();

                foreach (var vscodeInstance in VSCodeInstances.Instances)
                {
                    // storage.json contains opened Workspaces
                    var vscode_storage = Path.Combine(vscodeInstance.AppData, "storage.json");

                    // User/globalStorage/state.vscdb - history.recentlyOpenedPathsList - vscode v1.64 or later
                    var vscode_storage_db = Path.Combine(vscodeInstance.AppData, "User/globalStorage/state.vscdb");

                    if (File.Exists(vscode_storage))
                    {
                        var storageResults = GetWorkspacesInJson(vscodeInstance, vscode_storage);
                        results.AddRange(storageResults);
                    }

                    if (File.Exists(vscode_storage_db))
                    {
                        var storageDbResults = GetWorkspacesInVscdb(vscodeInstance, vscode_storage_db);
                        results.AddRange(storageDbResults);
                    }
                }

                return results;
            }
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
