// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ProtectedStorage;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
public sealed class ProtocolTests
{
    [TestMethod]
    public async Task UninitializedStateRequiresExplicitMigrationSuppressionPolicy()
    {
        var client = new ProtectedStoreClient(new StateTransport("""
            {"errorCode":"None","state":"Uninitialized","autoImportSuppressed":true}
            """));
        Assert.IsTrue((await client.GetStateAsync("workspaces.repository")).AutoImportSuppressed);

        client = new ProtectedStoreClient(new StateTransport("""
            {"errorCode":"None","state":"Uninitialized"}
            """));
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => client.GetStateAsync("workspaces.repository"));
        Assert.AreEqual("IncompatibleVersion", error.ErrorCode);
    }

    [TestMethod]
    public void HeaderUsesLittleEndianScalarsAndNetworkOrderUuid()
    {
        using var metadata = JsonDocument.Parse("{}");
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var frame = new StorageFrame(4, id, metadata.RootElement.Clone(), [0, 255, 23]);
        var encoded = PipeProtocol.Encode(frame);
        Assert.AreEqual("PTPS", Encoding.ASCII.GetString(encoded, 0, 4));
        Assert.AreEqual(36u, BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(12)));
        CollectionAssert.AreEqual(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"), encoded.AsSpan(20, 16).ToArray());
        var decoded = PipeProtocol.Decode(encoded);
        Assert.AreEqual(id, decoded.RequestId);
        Assert.AreEqual(4u, decoded.Command);
        CollectionAssert.AreEqual(frame.Bytes, decoded.Bytes);
    }

    [TestMethod]
    public async Task FragmentedReadsAndTruncationAreHandled()
    {
        using var document = JsonDocument.Parse("{\"target\":\"workspaces.repository\"}");
        var bytes = PipeProtocol.Encode(new StorageFrame(3, Guid.NewGuid(), document.RootElement.Clone(), [1, 2, 3]));
        for (int length = 0; length < bytes.Length; length++)
        {
            using var truncated = new MemoryStream(bytes, 0, length);
            await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => PipeProtocol.ReadAsync(truncated));
        }

        using var complete = new MemoryStream(bytes);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, (await PipeProtocol.ReadAsync(complete)).Bytes);
    }

    [TestMethod]
    public void MalformedFrameCorpusIsRejectedBeforeAllocation()
    {
        using var document = JsonDocument.Parse("{}");
        var valid = PipeProtocol.Encode(new StorageFrame(3, Guid.NewGuid(), document.RootElement.Clone(), []));
        foreach (int offset in new[] { 0, 4, 6, 8, 12, 16, 36 })
        {
            var mutated = valid.ToArray();
            mutated[offset] = 255;
            Assert.ThrowsExactly<InvalidDataException>(() => PipeProtocol.Decode(mutated), $"Offset {offset}");
        }

        var random = new Random(1149);
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            byte[] fuzz = new byte[random.Next(0, 256)];
            random.NextBytes(fuzz);
            Assert.ThrowsExactly<InvalidDataException>(() => PipeProtocol.Decode(fuzz));
        }
    }

    [TestMethod]
    public async Task ClientUsesNativeCasContractAndRejectsMismatchedResponse()
    {
        var transport = new RecordingTransport();
        var client = new ProtectedStoreClient(transport);
        var expected = new Revision(Guid.NewGuid(), 8);
        var request = new WriteRequest(Guid.NewGuid(), "workspaces.repository", expected, "workspaces.v1", [12]);
        await client.PutBlobAsync(request);
        Assert.AreEqual("IfRevision", transport.Request!.Metadata.GetProperty("condition").GetProperty("kind").GetString());
        Assert.AreEqual("8", transport.Request.Metadata.GetProperty("condition").GetProperty("expected").GetProperty("sequence").GetString());
        transport.Mismatch = true;
        var mismatch = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => client.PutBlobAsync(request));
        Assert.AreEqual("OutcomeUnknown", mismatch.ErrorCode);
        Assert.AreEqual(request.OperationId, mismatch.OperationId);
    }

    [TestMethod]
    public async Task MalformedMutationResponsesPreserveTheOriginalOperation()
    {
        var operation = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var request = new WriteRequest(operation, "workspaces.repository", null, "workspaces.v1", [1]);
        string[] responses =
        [
            """{"errorCode":"None","operationId":"00112233-4455-6677-8899-aabbccddeeff","outcome":"Committed"}""",
            """{"errorCode":"RevisionConflict"}""",
            """{"errorCode":"Timeout","nativeCode":1460,"retryClass":"QueryOutcome","messageKey":"ProtectedStorage.Timeout","operationId":"00112233-4455-6677-8899-aabbccddeeff"}""",
        ];
        foreach (var response in responses)
        {
            var client = new ProtectedStoreClient(new StateTransport(response));
            var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => client.PutBlobAsync(request));
            Assert.AreEqual("OutcomeUnknown", error.ErrorCode);
            Assert.AreEqual(operation, error.OperationId);
        }
    }

    [TestMethod]
    [DataRow("RevisionConflict")]
    [DataRow("OwnerContextRequired")]
    public async Task ValidatedExplicitServerRejectionRemainsKnown(string code)
    {
        string response = $$"""
            {"errorCode":"{{code}}","nativeCode":13,"retryClass":"None","messageKey":"ProtectedStorage.{{code}}","operationId":"00112233-4455-6677-8899-aabbccddeeff"}
            """;
        var operation = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var client = new ProtectedStoreClient(new StateTransport(response));
        var error = await Assert.ThrowsExactlyAsync<ProtectedStorageException>(() => client.PutBlobAsync(new WriteRequest(operation, "workspaces.repository", null, "workspaces.v1", [1])));
        Assert.AreEqual(code, error.ErrorCode);
        Assert.AreEqual(operation, error.OperationId);
    }

    [TestMethod]
    public async Task TransientUpdateUsesCommandTenAndTheReadRevision()
    {
        var transport = new RecordingTransport();
        var client = new ProtectedStoreClient(transport);
        var id = Guid.NewGuid();
        var operation = Guid.NewGuid();
        await client.UpdateTransientAsync("workspaces.preview", id, operation, new Revision(id, 8), [12], "workspaces.v1");
        Assert.AreEqual(10u, transport.Request!.Command);
        Assert.AreEqual(id, transport.Request.Metadata.GetProperty("id").GetGuid());
        Assert.AreEqual("8", transport.Request.Metadata.GetProperty("condition").GetProperty("expected").GetProperty("sequence").GetString());
        Assert.AreEqual(10u, PipeProtocol.Decode(PipeProtocol.Encode(transport.Request)).Command);
    }

    private sealed class StateTransport(string metadata) : IProtectedStorageTransport
    {
        public Task<StorageFrame> ExchangeAsync(StorageFrame request, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(metadata);
            return Task.FromResult(new StorageFrame(request.Command, request.RequestId, document.RootElement.Clone(), []));
        }
    }

    private sealed class RecordingTransport : IProtectedStorageTransport
    {
        public StorageFrame? Request { get; private set; }

        public bool Mismatch { get; set; }

        public Task<StorageFrame> ExchangeAsync(StorageFrame request, CancellationToken cancellationToken)
        {
            Request = request;
            string epoch = request.Command == 10 ? request.Metadata.GetProperty("id").GetString()! : "00112233-4455-6677-8899-aabbccddeeff";
            using var metadata = JsonDocument.Parse($$$"""
                {"errorCode":"None","operationId":"{{{request.Metadata.GetProperty("operationId").GetString()}}}",
                "id":"{{{epoch}}}","outcome":"Committed","revision":{"epoch":"{{{epoch}}}","sequence":"9"}}
                """);
            return Task.FromResult(new StorageFrame(request.Command, Mismatch ? Guid.NewGuid() : request.RequestId, metadata.RootElement.Clone(), []));
        }
    }
}
