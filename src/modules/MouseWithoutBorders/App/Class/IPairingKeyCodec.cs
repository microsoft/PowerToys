// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders.Class;

internal interface IPairingKeyCodec
{
    string Encrypt(string value);

    string Decrypt(string value);

    bool IsEncrypted(string value);
}
