// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Class;

internal abstract class PairingKeyCodec : IPairingKeyCodec
{
    internal const string EncryptedPrefix = "mwb-dpapi:v1:";

    public bool IsEncrypted(string value) => IsEncryptedValue(value);

    public abstract string Encrypt(string value);

    public abstract string Decrypt(string value);

    // Reserve the whole family, including damaged and unsupported versions.
    internal static bool IsEncryptedValue(string value) =>
        value?.StartsWith("mwb-dpapi:", StringComparison.OrdinalIgnoreCase) == true;

    // Envelope recognition does not establish that the native provider can decrypt it.
    internal static bool HasValidEncryptedFormat(string value)
    {
        if (value?.StartsWith(EncryptedPrefix, StringComparison.Ordinal) != true)
        {
            return false;
        }

        try
        {
            var payload = value.Substring(EncryptedPrefix.Length);
            var bytes = Convert.FromBase64String(payload);
            return bytes.Length > 0 && string.Equals(Convert.ToBase64String(bytes), payload, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static bool IsOrdinaryKey(string value) =>
        !string.IsNullOrWhiteSpace(value) && !IsEncryptedValue(value);
}
