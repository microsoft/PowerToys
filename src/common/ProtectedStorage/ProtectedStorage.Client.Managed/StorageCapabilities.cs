// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.ProtectedStorage;

public sealed record StorageCapabilities(
    uint ProtocolMajor,
    uint ProtocolMinor,
    int MaximumBlobBytes,
    int MaximumMetadataBytes,
    Version Release,
    bool Maintenance,
    bool RecoveryRequired,
    IReadOnlySet<string> Features)
{
    public void RequireReady(int requiredBlobBytes, IEnumerable<string> requiredFeatures)
    {
        if (ProtocolMajor != 1 || ProtocolMinor != 0 ||
            MaximumBlobBytes < requiredBlobBytes || MaximumMetadataBytes < 8192 ||
            requiredFeatures.Any(feature => !Features.Contains(feature)))
        {
            throw new ProtectedStorageException("IncompatibleVersion");
        }

        if (RecoveryRequired)
        {
            throw new ProtectedStorageException("RecoveryRequired");
        }

        if (Maintenance)
        {
            throw new ProtectedStorageException("BusyMaintenance");
        }
    }
}
