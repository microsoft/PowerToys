// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ProtectedStorage;
using WorkspacesCsharpLibrary.Data;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
public sealed class RepositoryTests
{
    [TestMethod]
    public async Task ExistingOwnerRuntimeIsSynchronizedThenCapabilitiesAreRechecked()
    {
        var client = new FakeClient();
        client.Capabilities = client.Capabilities with { Release = new Version(0, 0, 0, 0), ProtocolMinor = 1 };
        var setup = new FakeSetup(() => client.Capabilities = client.Capabilities with { Release = ProtectedStoreClient.ReleaseVersion, ProtocolMinor = 0 });
        var repository = new WorkspacesRepository(client, new FakeSource("{\"workspaces\":[]}"), setup);
        await repository.EnsureReadyAsync();
        Assert.AreEqual(1, setup.Calls);
        Assert.IsTrue(client.CapabilityQueries >= 2);
    }

    [TestMethod]
    public async Task SuccessfulSetupReceiptDoesNotAuthorizeAnIncompatibleRuntime()
    {
        var client = new FakeClient();
        client.Capabilities = client.Capabilities with { Release = new Version(0, 0, 0, 0), ProtocolMajor = 2 };
        var source = new FakeSource("{\"workspaces\":[]}");
        var repository = new WorkspacesRepository(client, source, new FakeSetup(() => { }));
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.EnsureReadyAsync());
        Assert.AreEqual("IncompatibleVersion", error.ErrorCode);
        Assert.AreEqual(0, source.Reads);
        Assert.AreEqual(0, client.Writes);
    }

    [TestMethod]
    public async Task CompatibleReleaseSkewUsesDataAuthorizationNotExeVersionEquality()
    {
        var client = new FakeClient { Initialized = true };
        client.Capabilities = client.Capabilities with { Release = new Version(99, 0, 0, 0) };
        var setup = new FakeSetup(() => Assert.Fail("A compatible runtime must not cause maintenance."));
        var repository = new WorkspacesRepository(client, new FakeSource("unused"), setup);
        await repository.EnsureReadyAsync();
        await repository.LoadAsync();
        Assert.AreEqual(0, setup.Calls);

        client.DenyDataAccess = true;
        client.Capabilities = client.Capabilities with { Release = ProtectedStoreClient.ReleaseVersion };
        var source = new FakeSource("{\"workspaces\":[]}");
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => new WorkspacesRepository(client, source, setup).EnsureReadyAsync());
        Assert.AreEqual("Unauthorized", error.ErrorCode);
        Assert.AreEqual(0, source.Reads);
        Assert.AreEqual(0, client.Writes);
    }

    [TestMethod]
    public async Task MissingFeatureAtTheSameReleaseDoesNotTriggerMaintenanceOrMigration()
    {
        var client = new FakeClient { Initialized = true };
        client.Capabilities = client.Capabilities with { Features = new HashSet<string>() };
        var setup = new FakeSetup(() => Assert.Fail("Do not replay maintenance for the same release."));
        var source = new FakeSource("{\"workspaces\":[]}");
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => new WorkspacesRepository(client, source, setup).EnsureReadyAsync());
        Assert.AreEqual("IncompatibleVersion", error.ErrorCode);
        Assert.AreEqual(0, setup.Calls);
        Assert.AreEqual(0, source.Reads);
        Assert.AreEqual(0, client.Writes);
    }

    [TestMethod]
    public async Task OlderUnauthorizedRuntimeCanSynchronizeOnceThenMustAuthorizeData()
    {
        var client = new FakeClient { Initialized = true, DenyDataAccess = true };
        client.Capabilities = client.Capabilities with { Release = new Version(0, 0, 0, 0) };
        var setup = new FakeSetup(() => client.DenyDataAccess = false);
        await new WorkspacesRepository(client, new FakeSource("unused"), setup).EnsureReadyAsync();
        Assert.AreEqual(1, setup.Calls);
    }

    [TestMethod]
    public void SharedNativeManagedValidationVectorsAgree()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "StorageValidationVectors.json")));
        foreach (var vector in document.RootElement.EnumerateArray())
        {
            bool accepted = true;
            try
            {
                WorkspacesValidator.Parse(Encoding.UTF8.GetBytes(vector.GetProperty("document").GetString()!));
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
                accepted = false;
            }

            Assert.AreEqual(vector.GetProperty("valid").GetBoolean(), accepted, vector.GetRawText());
        }
    }

    [TestMethod]
    public void FuzzMalformedWorkspaceBytesFailWithoutPartialResults()
    {
        var random = new Random(34901);
        for (int index = 0; index < 2000; index++)
        {
            var bytes = new byte[random.Next(0, 1024)];
            random.NextBytes(bytes);
            try
            {
                var projects = WorkspacesValidator.Parse(bytes);
                Assert.Fail("Random non-document input was accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
        }
    }

    [TestMethod]
    public async Task CorruptMigrationDoesNotWriteOrDeleteAnySource()
    {
        var client = new FakeClient();
        var source = new FakeSource("""
            {"workspaces":[{"id":"{00112233-4455-6677-8899-AABBCCDDEEFF}","name":"Valid first item","creation-time":0,"applications":[],"monitor-configuration":[]},{}]}
            """);
        var repository = new WorkspacesRepository(client, source);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => repository.EnsureReadyAsync());
        Assert.AreEqual(0, client.Writes);
        Assert.AreEqual(0, source.Deletes);
    }

    [TestMethod]
    public async Task InitializationIsConditionalAndDeletesOnlyAfterDurableCommit()
    {
        var client = new FakeClient();
        var source = new FakeSource("{\"workspaces\":[]}");
        var repository = new WorkspacesRepository(client, source);
        var snapshot = await repository.EnsureReadyAsync();
        Assert.IsNull(client.LastWrite!.Expected);
        Assert.IsNotNull(client.LastWrite.MigrationSource);
        Assert.AreEqual(1, client.Writes);
        Assert.AreEqual(1, source.Deletes);
        Assert.AreEqual(1, client.Acknowledgements);
        Assert.IsFalse(snapshot.SourceCleanupPending);
    }

    [TestMethod]
    public async Task InitializedStoreNeverReadsLegacyEvenWhenItIsCorrupt()
    {
        var client = new FakeClient { Initialized = true };
        var source = new FakeSource("corrupt");
        await new WorkspacesRepository(client, source).EnsureReadyAsync();
        Assert.AreEqual(0, source.Reads);
        Assert.AreEqual(0, client.Writes);
    }

    [TestMethod]
    public async Task PurgeSuppressionNeverReadsLegacyButAllowsExplicitImport()
    {
        var client = new FakeClient { AutoImportSuppressed = true };
        var source = new FakeSource("corrupt old file must not be read");
        var repository = new WorkspacesRepository(client, source);
        var snapshot = await repository.EnsureReadyAsync();
        Assert.AreEqual(0, snapshot.Projects.Count);
        Assert.AreEqual(0, source.Reads);
        Assert.AreEqual(0, source.Deletes);
        Assert.IsNull(client.LastWrite!.MigrationSource);

        string path = Path.Combine(Directory.GetCurrentDirectory(), $"explicit-import-{Guid.NewGuid():N}.json");
        try
        {
            const string imported = """
                {"workspaces":[{"id":"{00112233-4455-6677-8899-AABBCCDDEEFF}","name":"Explicit import","creation-time":0,"applications":[],"monitor-configuration":[]}]}
                """;
            File.WriteAllText(path, imported);
            await repository.ImportAsync(path, snapshot.Revision);
            Assert.AreEqual(1, (await repository.LoadAsync()).Projects.Count);
            Assert.AreEqual(snapshot.Revision, client.LastWrite.Expected);
            Assert.AreEqual(0, source.Reads);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LosingInitializerNeverDeletesAnUnacceptedSource()
    {
        var client = new FakeClient { LoseInitializationRace = true };
        var source = new FakeSource("{\"workspaces\":[]}");
        await new WorkspacesRepository(client, source).EnsureReadyAsync();
        Assert.AreEqual(1, client.Writes);
        Assert.AreEqual(0, source.Deletes);
        Assert.AreEqual(0, client.Acknowledgements);
    }

    [TestMethod]
    public async Task PreviewCancellationReleasesOnlyItsReference()
    {
        var client = new FakeClient();
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var project = new ProjectWrapper
        {
            Id = Guid.NewGuid().ToString("B"),
            Name = "Preview",
            Applications = [],
            MonitorConfiguration = [],
        };
        var first = await repository.CreatePreviewAsync(project);
        var second = await repository.CreatePreviewAsync(project);
        await repository.ReleasePreviewAsync(first);
        Assert.AreEqual(project.Id, (await repository.ReadPreviewAsync(second)).Id);
        await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.ReadPreviewAsync(first));
        Assert.AreEqual(0, client.Writes);
    }

    [TestMethod]
    public async Task PreviewUpdateRetriesTheSameCasOperationAfterUnknownOutcome()
    {
        var client = new FakeClient { DisconnectTransientAfterCommit = true };
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var project = new ProjectWrapper { Id = Guid.NewGuid().ToString("B"), Name = "Before", Applications = [], MonitorConfiguration = [] };
        var id = await repository.CreatePreviewAsync(project);
        await repository.ReadPreviewAsync(id);
        project.Name = "After";
        await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.UpdatePreviewAsync(id, project));
        await repository.UpdatePreviewAsync(id, project);
        Assert.AreEqual(2, client.TransientRequests);
        Assert.AreEqual(1, client.TransientWrites);
        Assert.AreEqual("After", (await repository.ReadPreviewAsync(id)).Name);
    }

    [TestMethod]
    public async Task PreviewUpdateRejectsAConcurrentRevisionChange()
    {
        var client = new FakeClient();
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var project = new ProjectWrapper { Id = Guid.NewGuid().ToString("B"), Name = "Before", Applications = [], MonitorConfiguration = [] };
        var id = await repository.CreatePreviewAsync(project);
        await repository.ReadPreviewAsync(id);
        project.Name = "Concurrent";
        await client.UpdateTransientAsync(WorkspacesRepository.PreviewTarget, id, Guid.NewGuid(), new Revision(id, 1), WorkspacesValidator.Serialize(new[] { project }, true), WorkspacesRepository.ContentSchema);
        project.Name = "Stale edit";
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.UpdatePreviewAsync(id, project));
        Assert.AreEqual("RevisionConflict", error.ErrorCode);
        Assert.AreEqual(1, client.TransientWrites);
    }

    [TestMethod]
    public async Task CleanupFailureRemainsPendingAndDoesNotRepeatMigration()
    {
        var client = new FakeClient();
        var source = new FakeSource("{\"workspaces\":[]}") { CanDelete = false };
        var repository = new WorkspacesRepository(client, source);
        Assert.IsTrue((await repository.EnsureReadyAsync()).SourceCleanupPending);
        source.CanDelete = true;
        Assert.IsTrue(await repository.RetrySourceCleanupAsync());
        Assert.AreEqual(1, client.Writes);
        Assert.AreEqual(1, source.Reads);
        Assert.AreEqual(1, client.Acknowledgements);
    }

    [TestMethod]
    public async Task MalformedCleanupReceiptNeverDeletesOrReimportsCommittedData()
    {
        var client = new FakeClient();
        var source = new FakeSource("{\"workspaces\":[]}") { InvalidReceipt = true };
        var snapshot = await new WorkspacesRepository(client, source).EnsureReadyAsync();
        Assert.IsTrue(snapshot.SourceCleanupPending);
        Assert.AreEqual(1, client.Writes);
        Assert.AreEqual(0, source.Deletes);
    }

    [TestMethod]
    public async Task UnknownSaveQueriesSameOperationWithoutDuplicateWrite()
    {
        var client = new FakeClient { Initialized = true, DisconnectAfterCommit = true };
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var before = await repository.LoadAsync();
        await repository.SaveAsync(before.Projects, before.Revision);
        Assert.AreEqual(1, client.Writes);
        Assert.AreEqual(client.LastWrite!.OperationId, client.Queried);
    }

    [TestMethod]
    public async Task RevisionConflictDoesNotRetryAnUnconditionalWrite()
    {
        var client = new FakeClient { Initialized = true, Conflict = true };
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var before = await repository.LoadAsync();
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.SaveAsync(before.Projects, before.Revision));
        Assert.AreEqual("RevisionConflict", error.ErrorCode);
        Assert.AreEqual(before.Revision, client.LastWrite!.Expected);
        Assert.AreEqual(1, client.Writes);
    }

    [TestMethod]
    public async Task CancellationAfterTransmissionRemainsAnUnknownOutcome()
    {
        var client = new FakeClient { Initialized = true, DisconnectAfterCommit = true, CancelQuery = true };
        var repository = new WorkspacesRepository(client, new FakeSource("unused"));
        var before = await repository.LoadAsync();
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => repository.SaveAsync(before.Projects, before.Revision));
        Assert.AreEqual("OutcomeUnknown", error.ErrorCode);
        Assert.AreEqual(client.LastWrite!.OperationId, error.OperationId);
        Assert.AreEqual(1, client.Writes);
    }

    [TestMethod]
    public void InvalidInputAndDuplicatePropertiesRejectTheWholeDocument()
    {
        foreach (string input in new[] { "{}", "[]", "{\"workspaces\":null}", "{\"workspaces\":[],\"workspaces\":[]}", "{\"workspaces\":[null]}", "{\"workspaces\":[{\"id\":\"not-a-guid\"}]}" })
        {
            Assert.ThrowsExactly<InvalidDataException>(() => WorkspacesValidator.Parse(Encoding.UTF8.GetBytes(input)));
        }
    }

    [TestMethod]
    public void LegacyAccessRejectsUnfilterableElevationOrDeletesOnlyMatchingObject()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"workspace-source-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "workspaces.json");
        try
        {
            File.WriteAllText(path, "{\"workspaces\":[]}");
            var source = new LegacyWorkspaceSource(path);
            LegacyWorkspaceSnapshot snapshot;
            try
            {
                snapshot = source.OpenStableSnapshot(Guid.NewGuid());
            }
            catch (ProtectedStorageException exception) when (exception.ErrorCode == "OwnerContextRequired")
            {
                Assert.AreEqual("{\"workspaces\":[]}", File.ReadAllText(path));
                var rejected = Assert.ThrowsExactly<ProtectedStorageException>(() => source.DeleteIfUnchanged("{}"));
                Assert.AreEqual("OwnerContextRequired", rejected.ErrorCode);
                Assert.IsTrue(File.Exists(path));
                return;
            }

            File.Move(path, path + ".old");
            File.WriteAllText(path, "{\"workspaces\":[]}");
            Assert.IsFalse(source.DeleteIfUnchanged(snapshot.Receipt));
            Assert.IsTrue(File.Exists(path));
            var replacement = source.OpenStableSnapshot(Guid.NewGuid());
            File.WriteAllText(path, "{\"WorkspaceS\":[]}");
            Assert.IsFalse(source.DeleteIfUnchanged(replacement.Receipt));
            File.WriteAllText(path, "{\"workspaces\":[]}");
            Assert.IsTrue(source.DeleteIfUnchanged(replacement.Receipt));
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class FakeSource(string json) : ILegacyWorkspaceSource
    {
        public int Reads { get; private set; }

        public int Deletes { get; private set; }

        public bool CanDelete { get; set; } = true;

        public bool InvalidReceipt { get; set; }

        public LegacyWorkspaceSnapshot OpenStableSnapshot(Guid migrationId)
        {
            Reads++;
            return new LegacyWorkspaceSnapshot(
                Encoding.UTF8.GetBytes(json),
                InvalidReceipt ? "{\"MigrationId\":17}" : "{\"MigrationId\":\"" + migrationId.ToString("D") + "\",\"source\":\"bound-test-snapshot\"}");
        }

        public bool DeleteIfUnchanged(string receipt)
        {
            Deletes++;
            return CanDelete;
        }
    }

    private sealed class FakeClient : IProtectedStoreClient
    {
        private readonly Guid epoch = Guid.NewGuid();
        private readonly Dictionary<Guid, byte[]> previews = new();
        private readonly Dictionary<Guid, ulong> previewSequences = new();
        private readonly Dictionary<Guid, (Guid Id, Revision Expected, byte[] Bytes, WriteResult Result)> previewReceipts = new();
        private byte[] bytes = Encoding.UTF8.GetBytes("{\"workspaces\":[]}");
        private ulong sequence = 1;

        public StorageCapabilities Capabilities { get; set; } = new(1, 0, PipeProtocol.MaximumBlob, PipeProtocol.MaximumMetadata, ProtectedStoreClient.ReleaseVersion, false, false, new HashSet<string> { "cas", "write-query", "source-receipt", "transient", "transient-cas", "purge-suppression" });

        public int CapabilityQueries { get; private set; }

        public bool Initialized { get; set; }

        public bool AutoImportSuppressed { get; set; }

        public bool DenyDataAccess { get; set; }

        public bool DisconnectAfterCommit { get; set; }

        public bool Conflict { get; set; }

        public bool LoseInitializationRace { get; set; }

        public bool CancelQuery { get; set; }

        public bool DisconnectTransientAfterCommit { get; set; }

        public int TransientRequests { get; private set; }

        public int TransientWrites { get; private set; }

        public int Writes { get; private set; }

        public int Acknowledgements { get; private set; }

        public WriteRequest? LastWrite { get; private set; }

        public Guid Queried { get; private set; }

        public Task<StorageCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            CapabilityQueries++;
            return Task.FromResult(Capabilities);
        }

        public Task<TargetInfo> GetStateAsync(string target, CancellationToken cancellationToken = default)
        {
            if (DenyDataAccess)
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            return Task.FromResult(new TargetInfo(Initialized ? "Initialized" : "Uninitialized", Initialized ? new Revision(epoch, sequence) : null, LastWrite?.MigrationSource, Acknowledgements > 0, LastWrite?.OperationId, AutoImportSuppressed));
        }

        public Task<BlobValue> GetBlobAsync(string target, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BlobValue(new Revision(epoch, sequence), WorkspacesRepository.ContentSchema, bytes));

        public Task<WriteResult> PutBlobAsync(WriteRequest request, CancellationToken cancellationToken = default)
        {
            LastWrite = request;
            Writes++;
            if (LoseInitializationRace)
            {
                Initialized = true;
                LastWrite = null;
                throw new ProtectedStorageException("AlreadyInitialized");
            }

            if (Conflict)
            {
                throw new ProtectedStorageException("RevisionConflict");
            }

            Initialized = true;
            bytes = request.Bytes;
            sequence++;
            if (DisconnectAfterCommit)
            {
                throw new ProtectedStorageException("OutcomeUnknown", request.OperationId);
            }

            return Task.FromResult(new WriteResult(request.OperationId, new Revision(epoch, sequence), "Committed"));
        }

        public Task<WriteResult> QueryWriteAsync(string target, Guid operationId, CancellationToken cancellationToken = default)
        {
            Queried = operationId;
            if (CancelQuery)
            {
                throw new OperationCanceledException();
            }

            return Task.FromResult(new WriteResult(operationId, new Revision(epoch, sequence), "Committed"));
        }

        public Task AcknowledgeSourceCleanupAsync(string target, Guid operationId, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(LastWrite!.OperationId, operationId);
            Acknowledgements++;
            return Task.CompletedTask;
        }

        public Task<Guid> CreateTransientAsync(string target, Guid operationId, byte[] content, string contentSchema, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(WorkspacesRepository.PreviewTarget, target);
            var id = Guid.NewGuid();
            previews.Add(id, content);
            previewSequences.Add(id, 1);
            return Task.FromResult(id);
        }

        public Task<BlobValue> GetTransientAsync(string target, Guid id, CancellationToken cancellationToken = default)
        {
            if (!previews.TryGetValue(id, out var content))
            {
                throw new ProtectedStorageException("NotFound");
            }

            return Task.FromResult(new BlobValue(new Revision(id, previewSequences[id]), WorkspacesRepository.ContentSchema, content));
        }

        public Task<WriteResult> UpdateTransientAsync(string target, Guid id, Guid operationId, Revision expected, byte[] content, string contentSchema, CancellationToken cancellationToken = default)
        {
            TransientRequests++;
            if (previewReceipts.TryGetValue(operationId, out var receipt))
            {
                Assert.AreEqual(receipt.Id, id);
                Assert.AreEqual(receipt.Expected, expected);
                CollectionAssert.AreEqual(receipt.Bytes, content);
                return Task.FromResult(receipt.Result);
            }

            if (!previews.ContainsKey(id))
            {
                throw new ProtectedStorageException("NotFound");
            }

            if (expected != new Revision(id, previewSequences[id]))
            {
                throw new ProtectedStorageException("RevisionConflict");
            }

            TransientWrites++;
            previews[id] = content;
            var result = new WriteResult(operationId, new Revision(id, ++previewSequences[id]), "Committed");
            previewReceipts.Add(operationId, (id, expected, content, result));
            if (DisconnectTransientAfterCommit)
            {
                throw new ProtectedStorageException("OutcomeUnknown", operationId);
            }

            return Task.FromResult(result);
        }

        public Task DeleteTransientAsync(string target, Guid id, CancellationToken cancellationToken = default)
        {
            previews.Remove(id);
            previewSequences.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSetup(Action synchronize) : IProtectedStorageSetupClient
    {
        public int Calls { get; private set; }

        public Task<MaintenanceResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            synchronize();
            return Task.FromResult(new MaintenanceResult(Guid.NewGuid(), "Ready", 0, "None", false, false));
        }

        public Task<MaintenanceResult> EnsureReadyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MaintenanceResult> InspectAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MaintenanceResult> RepairAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MaintenanceResult> RepairBootstrapWithAuthorizationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MaintenanceResult> RetryFailedOperationAsync(Guid operationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
