// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Uses current-user DPAPI; it does not prevent data replacement by another same-user process.</summary>
public sealed class CurrentUserDataProtector : IAtRestDataProtector
{
    private const string ProtectionDescriptor = "LOCAL=user";

    public async Task<byte[]> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var provider = new DataProtectionProvider(ProtectionDescriptor);
        var input = CryptographicBuffer.CreateFromByteArray(plaintext.ToArray());
        var output = await provider.ProtectAsync(input);

        cancellationToken.ThrowIfCancellationRequested();
        CryptographicBuffer.CopyToByteArray(output, out var protectedData);
        return protectedData;
    }

    public async Task<byte[]> UnprotectAsync(ReadOnlyMemory<byte> protectedData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var provider = new DataProtectionProvider();
        var input = CryptographicBuffer.CreateFromByteArray(protectedData.ToArray());
        var output = await provider.UnprotectAsync(input);

        cancellationToken.ThrowIfCancellationRequested();
        CryptographicBuffer.CopyToByteArray(output, out var plaintext);
        return plaintext;
    }
}
