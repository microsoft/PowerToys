// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Work-in-progress at-rest protection; not an authorization or same-user integrity boundary.</summary>
public interface IAtRestDataProtector
{
    Task<byte[]> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken = default);

    Task<byte[]> UnprotectAsync(ReadOnlyMemory<byte> protectedData, CancellationToken cancellationToken = default);
}
