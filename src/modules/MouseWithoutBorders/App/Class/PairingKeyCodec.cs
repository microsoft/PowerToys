// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Class;

internal abstract class PairingKeyCodec : IPairingKeyCodec
{
    internal const string EncryptedPrefix = "mwb-dpapi:";
    internal const string EncryptedVersion = "v1:";

    public bool IsEncrypted(string value) => IsEncryptedValue(value);

    public abstract string Encrypt(string value);

    public abstract string Decrypt(string value);

    // Reserve the whole family, including damaged and unsupported versions; only Decrypt validates the payload.
    internal static bool IsEncryptedValue(string value) =>
        value?.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase) == true;
}
