// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders.Class;

internal sealed class PassthroughPairingKeyCodec : PairingKeyCodec
{
    public override string Encrypt(string value) => value;

    public override string Decrypt(string value) => value;
}
