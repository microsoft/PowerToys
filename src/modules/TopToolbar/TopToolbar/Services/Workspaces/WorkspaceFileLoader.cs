// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class WorkspaceFileLoader
    {
        private readonly string _workspacesPath;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly JsonSerializerOptions _writeSerializerOptions;

        public WorkspaceFileLoader(string workspacesPath = null)
        {
            _workspacesPath = string.IsNullOrWhiteSpace(workspacesPath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    "Workspaces",
                    "workspaces.json")
                : workspacesPath;

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            _writeSerializerOptions = new JsonSerializerOptions(_serializerOptions)
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }

        public async Task<IReadOnlyList<WorkspaceDefinition>> LoadAllAsync(CancellationToken cancellationToken)
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (document?.Workspaces == null || document.Workspaces.Count == 0)
            {
                return Array.Empty<WorkspaceDefinition>();
            }

            return document.Workspaces;
        }

        public async Task<WorkspaceDefinition> LoadByIdAsync(string workspaceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                return null;
            }

            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (document?.Workspaces == null || document.Workspaces.Count == 0)
            {
                return null;
            }

            return document.Workspaces.FirstOrDefault(ws => string.Equals(ws.Id, workspaceId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task SaveWorkspaceAsync(WorkspaceDefinition workspace, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workspace);

            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false) ?? new WorkspaceDocument();
            document.Workspaces ??= new List<WorkspaceDefinition>();

            document.Workspaces.RemoveAll(ws =>
                string.Equals(ws.Id, workspace.Id, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(ws.Name) && !string.IsNullOrWhiteSpace(workspace.Name) && string.Equals(ws.Name, workspace.Name, StringComparison.OrdinalIgnoreCase)));

            document.Workspaces.Insert(0, workspace);

            var directory = Path.GetDirectoryName(_workspacesPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _workspacesPath + ".tmp";

            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, _writeSerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Copy(tempPath, _workspacesPath, overwrite: true);
            File.Delete(tempPath);
        }

        private async Task<WorkspaceDocument> ReadDocumentAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_workspacesPath))
            {
                return null;
            }

            try
            {
                await using var stream = new FileStream(_workspacesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return await JsonSerializer.DeserializeAsync<WorkspaceDocument>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
