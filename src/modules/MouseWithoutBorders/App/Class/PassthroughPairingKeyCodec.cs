// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Class;

internal sealed class PassthroughPairingKeyCodec : PairingKeyCodec
{
    public override string Encrypt(string value) => Decrypt(value);

    public override string Decrypt(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (IsEncrypted(value))
        {
            throw new ArgumentException("Expected an ordinary pairing key; pairing setup is required.", nameof(value));
        }

        return value;
    }
}
