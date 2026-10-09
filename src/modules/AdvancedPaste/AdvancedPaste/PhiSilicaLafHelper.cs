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
    private static bool _unlocked;

    /// <summary>
    /// Gets the status of the most recent <see cref="TryUnlock"/> attempt
    /// (e.g. Available, AvailableWithoutToken, Unavailable, or "Exception: ...").
    /// Exposed so callers can surface the real LAF result for diagnostics; the
    /// generic "Access is denied" from downstream model calls does not reveal it.
    /// </summary>
    public static string LastUnlockStatus { get; private set; } = "NotAttempted";

    public static bool TryUnlock()
    {
        // Only cache a successful unlock. Negative results (Unavailable, Unknown, exceptions)
        // are often transient — e.g., AI feature stack not yet initialized after sign-in or
        // sparse identity not fully applied to a freshly-started process — and retrying on
        // the next call lets AP recover without restart.
        if (_unlocked)
        {
            return true;
        }

        lock (_lock)
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
