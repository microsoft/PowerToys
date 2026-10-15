// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace MouseWithoutBorders.Class;

internal sealed class PassthroughPairingKeyCodec : IPairingKeyCodec
{
    public bool IsEncrypted(string value) => MouseWithoutBordersPairingHelper.IsEncrypted(value);

    public string Encrypt(string value) => Validate(value);

    public string Decrypt(string value) => Validate(value);

    private static string Validate(string value)
    {
        if (!MouseWithoutBordersPairingHelper.IsOrdinaryKey(value))
        {
            throw new ArgumentException("Expected an ordinary pairing key; pairing setup is required.", nameof(value));
        }

        return value;
    }
}
