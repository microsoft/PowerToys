// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Unlocks the Windows AI LanguageModel limited-access feature for Phi Silica.
/// </summary>
public static class PhiSilicaLafHelper
{
    private const string FeatureId = "com.microsoft.windows.ai.languagemodel";

    private static readonly object SyncLock = new();
    private static bool _unlocked;

    /// <summary>
    /// Gets the status of the most recent <see cref="TryUnlock"/> attempt.
    /// </summary>
    public static string LastUnlockStatus { get; private set; } = "NotAttempted";

    public static bool TryUnlock()
    {
        if (_unlocked)
        {
            return true;
        }

        lock (SyncLock)
        {
            if (_unlocked)
            {
                return true;
            }

            try
            {
                var access = LimitedAccessFeatures.TryUnlockFeature(
                    FeatureId,
                    PhiSilicaLafCredentials.Token,
                    PhiSilicaLafCredentials.Attestation + " has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.");

                _unlocked = access.Status == LimitedAccessFeatureStatus.Available
                         || access.Status == LimitedAccessFeatureStatus.AvailableWithoutToken;

                LastUnlockStatus = access.Status.ToString();
                Debug.WriteLine($"Phi Silica LAF unlock status: {access.Status}");
            }
            catch (Exception ex)
            {
                LastUnlockStatus = "Exception: " + ex.Message;
                Debug.WriteLine($"Phi Silica LAF unlock failed: {ex.Message}");
                _unlocked = false;
            }

            return _unlocked;
        }
    }
}
