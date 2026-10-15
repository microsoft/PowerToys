// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities;

public static class MouseWithoutBordersPairingHelper
{
    public const string EncryptedPrefix = "mwb-dpapi:v1:";

    // Reserve the whole format family, including damaged and unsupported versions.
    public static bool IsEncrypted(string value) =>
        value?.StartsWith("mwb-dpapi:", StringComparison.OrdinalIgnoreCase) == true;

    public static bool HasValidEncryptedFormat(string value)
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

    public static bool IsOrdinaryKey(string value) =>
        !string.IsNullOrWhiteSpace(value) && !IsEncrypted(value) &&
        value != MouseWithoutBordersIpc.PairingConfiguredIndicator &&
        value != MouseWithoutBordersIpc.PairingSetupRequiredIndicator;

    public static bool CanManagePairing(bool isElevated, bool serviceEnabled) =>
        !serviceEnabled || isElevated;

    public static bool PairingSetupRequired(string persistedValue, bool serviceEnabled) =>
        serviceEnabled ? !HasValidEncryptedFormat(persistedValue) : !IsOrdinaryKey(persistedValue);

    // A service key is displayable only when supplied by a fresh, authorized RPC response.
    public static string GetDisplayValue(string persistedValue, bool isElevated, bool serviceEnabled, string freshServiceKey = null)
    {
        if (!serviceEnabled)
        {
            return IsOrdinaryKey(persistedValue) ? persistedValue : string.Empty;
        }

        if (isElevated && IsOrdinaryKey(freshServiceKey))
        {
            return freshServiceKey;
        }

        return HasValidEncryptedFormat(persistedValue) ? MouseWithoutBordersIpc.PairingConfiguredIndicator : string.Empty;
    }
}
