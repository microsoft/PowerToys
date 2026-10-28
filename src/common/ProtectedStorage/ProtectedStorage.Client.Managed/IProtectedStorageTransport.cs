// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public interface IProtectedStorageTransport
{
    Task<StorageFrame> ExchangeAsync(StorageFrame request, CancellationToken cancellationToken);
}
