// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace PowerToysExtension.Helpers;

internal static class PowerDisplayCommandIds
{
    private const string ApplyProfilePrefix = "com.microsoft.powertoys.powerDisplay.applyProfile.";

    internal static string BuildApplyProfileCommandId(int profileId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileId);

        return ApplyProfilePrefix + profileId.ToString(CultureInfo.InvariantCulture);
    }

    internal static bool TryParseApplyProfileCommandId(string id, out int profileId)
    {
        profileId = 0;
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(ApplyProfilePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var idText = id[ApplyProfilePrefix.Length..];
        if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId) ||
            parsedId <= 0 ||
            !string.Equals(idText, parsedId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return false;
        }

        profileId = parsedId;
        return true;
    }
}
