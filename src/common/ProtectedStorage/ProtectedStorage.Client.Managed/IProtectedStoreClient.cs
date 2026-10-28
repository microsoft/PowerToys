// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public interface IProtectedStoreClient
{
    Task<StorageCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    Task<TargetInfo> GetStateAsync(string target, CancellationToken cancellationToken = default);

    Task<BlobValue> GetBlobAsync(string target, CancellationToken cancellationToken = default);

    Task<WriteResult> PutBlobAsync(WriteRequest request, CancellationToken cancellationToken = default);

    Task<WriteResult> QueryWriteAsync(string target, Guid operationId, CancellationToken cancellationToken = default);

    Task AcknowledgeSourceCleanupAsync(string target, Guid operationId, CancellationToken cancellationToken = default);

    Task<Guid> CreateTransientAsync(string target, Guid operationId, byte[] bytes, string contentSchema, CancellationToken cancellationToken = default);

    Task<BlobValue> GetTransientAsync(string target, Guid id, CancellationToken cancellationToken = default);

    Task<WriteResult> UpdateTransientAsync(string target, Guid id, Guid operationId, Revision expected, byte[] bytes, string contentSchema, CancellationToken cancellationToken = default);

    Task DeleteTransientAsync(string target, Guid id, CancellationToken cancellationToken = default);
}
