// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace AdvancedPaste;

internal static class PhiSilicaLafHelper
{
    private const string FeatureId = "com.microsoft.windows.ai.languagemodel";

    private static readonly object _lock = new();
    private static bool _attempted;
    private static bool _unlocked;

    public static bool TryUnlock()
    {
        if (_attempted)
        {
            return _unlocked;
        }

        lock (_lock)
        {
            if (_attempted)
            {
                return _unlocked;
            }

            _attempted = true;

            try
            {
                var access = LimitedAccessFeatures.TryUnlockFeature(
                    FeatureId,
                    PhiSilicaLafCredentials.Token,
                    PhiSilicaLafCredentials.Attestation + " has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.");

                _unlocked = access.Status == LimitedAccessFeatureStatus.Available
                         || access.Status == LimitedAccessFeatureStatus.AvailableWithoutToken;

                Debug.WriteLine($"Phi Silica LAF unlock status: {access.Status}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Phi Silica LAF unlock failed: {ex.Message}");
                _unlocked = false;
            }

            return _unlocked;
        }
    }
}
