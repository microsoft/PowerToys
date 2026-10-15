// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders.Class;

internal interface IPairingKeyCodec
{
    /// <summary>Converts an input key to its complete stored representation.</summary>
    string Encrypt(string value);

    /// <summary>Converts a complete stored representation to a key, or throws if it cannot be used in this mode.</summary>
    string Decrypt(string value);

    bool IsEncrypted(string value);
}
