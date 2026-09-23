// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using PowerToys.ProtectedStorage;

namespace WorkspacesCsharpLibrary.Data;

public sealed class WorkspacesRepository
{
    public const string Target = "workspaces.repository";
    public const string PreviewTarget = "workspaces.preview";
    public const string ContentSchema = "workspaces.v1";
    private readonly IProtectedStoreClient client;
    private readonly ILegacyWorkspaceSource legacy;
    private readonly IProtectedStorageSetupClient setup;
    private readonly Dictionary<Guid, Revision> previewRevisions = new();
    private readonly Dictionary<Guid, PendingPreview> pendingPreviews = new();
    private WriteRequest pendingWrite;

    public WorkspacesRepository()
        : this(new ProtectedStoreClient(), new LegacyWorkspaceSource(), new ProtectedStorageSetupClient())
    {
    }

    public WorkspacesRepository(IProtectedStoreClient client, ILegacyWorkspaceSource legacy, IProtectedStorageSetupClient setup = null)
    {
        this.client = client;
        this.legacy = legacy;
        this.setup = setup;
    }

    public async Task<WorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        var blob = await client.GetBlobAsync(Target, cancellationToken).ConfigureAwait(false);
        CheckSchema(blob);
        return new WorkspaceSnapshot(WorkspacesValidator.Parse(blob.Bytes), blob.Revision);
    }

    public async Task<WorkspaceSnapshot> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        var state = await ReadInitializationStateAsync(cancellationToken).ConfigureAwait(false);
        if (pendingWrite != null)
        {
            await ResolvePendingAsync(cancellationToken).ConfigureAwait(false);
            state = await client.GetStateAsync(Target, cancellationToken).ConfigureAwait(false);
        }

        if (state.State == "RecoveryRequired")
        {
            throw new ProtectedStorageException("RecoveryRequired");
        }

        if (state.State == "Uninitialized")
        {
            var operation = Guid.NewGuid();
            var source = state.AutoImportSuppressed ? null : legacy.OpenStableSnapshot(operation);
            var projects = source == null ? new List<ProjectWrapper>() : WorkspacesValidator.Parse(source.Bytes, convertLegacy: true);
            JsonElement? receipt = null;
            if (source != null)
            {
                using var document = JsonDocument.Parse(source.Receipt);
                receipt = document.RootElement.Clone();
            }

            var request = new WriteRequest(operation, Target, null, ContentSchema, WorkspacesValidator.Serialize(projects), receipt);
            try
            {
                await CommitAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (ProtectedStorageException exception) when (exception.ErrorCode == "AlreadyInitialized")
            {
                // A losing initializer has no authority to delete its source snapshot.
            }
        }
        else if (state.State != "Initialized")
        {
            throw new ProtectedStorageException("InvalidPayload");
        }

        bool cleaned = await RetrySourceCleanupAsync(cancellationToken).ConfigureAwait(false);
        return (await LoadAsync(cancellationToken).ConfigureAwait(false)) with { SourceCleanupPending = !cleaned };
    }

    public async Task<bool> RetrySourceCleanupAsync(CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        var state = await client.GetStateAsync(Target, cancellationToken).ConfigureAwait(false);
        if (state.State != "Initialized")
        {
            throw new ProtectedStorageException(state.State == "RecoveryRequired" ? "RecoveryRequired" : "NotFound");
        }

        if (state.MigrationSource is not JsonElement source || state.CleanupAcknowledged)
        {
            return true;
        }

        try
        {
            if (state.InitializationOperationId is not Guid migrationId ||
                !source.TryGetProperty("MigrationId", out var receiptOperation) ||
                !receiptOperation.TryGetGuid(out var acceptedMigration) || acceptedMigration != migrationId)
            {
                throw new InvalidDataException("Missing committed migration receipt.");
            }

            if (!legacy.DeleteIfUnchanged(source.GetRawText()))
            {
                return false;
            }

            await client.AcknowledgeSourceCleanupAsync(Target, migrationId, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException or InvalidOperationException)
        {
            // The record remains authoritative and its receipt survives ordinary saves.
            return false;
        }
    }

    public async Task<Revision> SaveAsync(IReadOnlyList<ProjectWrapper> projects, Revision expected, CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(expected);
        var bytes = WorkspacesValidator.Serialize(projects);
        if (pendingWrite != null)
        {
            var previous = pendingWrite;
            var committed = await ResolvePendingAsync(cancellationToken).ConfigureAwait(false);
            if (previous.Expected != expected || !previous.Bytes.AsSpan().SequenceEqual(bytes))
            {
                throw new ProtectedStorageException("RevisionConflict", previous.OperationId);
            }

            return committed;
        }

        return await CommitAsync(new WriteRequest(Guid.NewGuid(), Target, expected, ContentSchema, bytes), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid> CreatePreviewAsync(ProjectWrapper project, CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        return await client.CreateTransientAsync(PreviewTarget, Guid.NewGuid(), WorkspacesValidator.Serialize(new[] { project }, true), ContentSchema, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectWrapper> ReadPreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        var blob = await client.GetTransientAsync(PreviewTarget, id, cancellationToken).ConfigureAwait(false);
        CheckSchema(blob);
        var project = WorkspacesValidator.Parse(blob.Bytes, true)[0];
        previewRevisions[id] = blob.Revision;
        return project;
    }

    public async Task UpdatePreviewAsync(Guid id, ProjectWrapper project, CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        var bytes = WorkspacesValidator.Serialize(new[] { project }, true);
        if (!pendingPreviews.TryGetValue(id, out var pending))
        {
            var blob = await client.GetTransientAsync(PreviewTarget, id, cancellationToken).ConfigureAwait(false);
            CheckSchema(blob);
            WorkspacesValidator.Parse(blob.Bytes, true);
            if (previewRevisions.TryGetValue(id, out var loaded) && loaded != blob.Revision)
            {
                throw new ProtectedStorageException("RevisionConflict");
            }

            pending = new PendingPreview(Guid.NewGuid(), blob.Revision, bytes);
            pendingPreviews.Add(id, pending);
        }
        else if (!pending.Bytes.AsSpan().SequenceEqual(bytes))
        {
            throw new ProtectedStorageException("OutcomeUnknown", pending.OperationId);
        }

        try
        {
            var result = await client.UpdateTransientAsync(PreviewTarget, id, pending.OperationId, pending.Expected, pending.Bytes, ContentSchema, cancellationToken).ConfigureAwait(false);
            if (result.Outcome != "Committed" || result.Revision == null)
            {
                throw new ProtectedStorageException("OutcomeUnknown", pending.OperationId);
            }

            previewRevisions[id] = result.Revision;
            pendingPreviews.Remove(id);
        }
        catch (ProtectedStorageException exception) when (exception.ErrorCode != "OutcomeUnknown")
        {
            pendingPreviews.Remove(id);
            throw;
        }
    }

    public async Task ReleasePreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await VerifyReadyAsync(cancellationToken).ConfigureAwait(false);
        await client.DeleteTransientAsync(PreviewTarget, id, cancellationToken).ConfigureAwait(false);
        previewRevisions.Remove(id);
        pendingPreviews.Remove(id);
    }

    public async Task<Revision> ImportAsync(string selectedPath, Revision expected, CancellationToken cancellationToken = default)
    {
        using var source = new FileStream(selectedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length <= 0 || source.Length > WorkspacesValidator.MaximumBytes)
        {
            throw new InvalidDataException("The import file is empty or too large.");
        }

        byte[] bytes = new byte[(int)source.Length];
        await source.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return await SaveAsync(WorkspacesValidator.Parse(bytes, convertLegacy: true), expected, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportAsync(string selectedPath, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = WorkspacesValidator.Serialize(snapshot.Projects);
        using var output = new FileStream(selectedPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        output.Flush(true);
    }

    private async Task<Revision> CommitAsync(WriteRequest request, CancellationToken cancellationToken)
    {
        pendingWrite = request;
        try
        {
            var result = await client.PutBlobAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Outcome != "Committed" || result.Revision == null)
            {
                throw new ProtectedStorageException("OutcomeUnknown", request.OperationId);
            }

            pendingWrite = null;
            return result.Revision;
        }
        catch (ProtectedStorageException exception) when (exception.ErrorCode == "OutcomeUnknown")
        {
            return await ResolvePendingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            pendingWrite = null;
            throw;
        }
    }

    private async Task<Revision> ResolvePendingAsync(CancellationToken cancellationToken)
    {
        var operationId = pendingWrite.OperationId;
        WriteResult result;
        try
        {
            result = await client.QueryWriteAsync(Target, operationId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            throw new ProtectedStorageException("OutcomeUnknown", operationId, innerException: exception);
        }

        if (result.Outcome == "NotCommitted")
        {
            var operation = pendingWrite.OperationId;
            pendingWrite = null;
            throw new ProtectedStorageException("RetryRequired", operation);
        }

        if (result.Outcome != "Committed" || result.Revision == null)
        {
            throw new ProtectedStorageException("OutcomeUnknown", pendingWrite.OperationId);
        }

        pendingWrite = null;
        return result.Revision;
    }

    private static void CheckSchema(BlobValue blob)
    {
        if (blob.ContentSchema != ContentSchema)
        {
            throw new InvalidDataException("Unsupported Workspaces schema. Update PowerToys before retrying.");
        }
    }

    private async Task<TargetInfo> ReadInitializationStateAsync(CancellationToken cancellationToken)
    {
        var capabilities = await client.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RequireWorkspaceCapabilities(capabilities);
            return await client.GetStateAsync(Target, cancellationToken).ConfigureAwait(false);
        }
        catch (ProtectedStorageException exception) when (
            setup != null && capabilities.Release < ProtectedStoreClient.ReleaseVersion &&
            !capabilities.Maintenance && !capabilities.RecoveryRequired &&
            exception.ErrorCode is "IncompatibleVersion" or "Unauthorized")
        {
            var result = await setup.SyncAsync(cancellationToken).ConfigureAwait(false);
            result.ThrowIfFailed();
            result.ThrowIfRestartRequired();

            // Re-check the real boundary; a maintenance receipt grants no data access.
            capabilities = await client.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            RequireWorkspaceCapabilities(capabilities);
            return await client.GetStateAsync(Target, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task VerifyReadyAsync(CancellationToken cancellationToken)
    {
        var capabilities = await client.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        RequireWorkspaceCapabilities(capabilities);
    }

    private static void RequireWorkspaceCapabilities(StorageCapabilities capabilities)
    {
        capabilities.RequireReady(WorkspacesValidator.MaximumBytes, ["cas", "write-query", "source-receipt", "transient", "transient-cas", "purge-suppression"]);
    }

    private sealed record PendingPreview(Guid OperationId, Revision Expected, byte[] Bytes);
}
