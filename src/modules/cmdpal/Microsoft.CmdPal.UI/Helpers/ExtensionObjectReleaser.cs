// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Core.Common.Helpers;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Releases the extension objects behind pages the user has left.
/// </summary>
internal static class ExtensionObjectReleaser
{
    // Long enough that the page the user just navigated to renders first, and
    // that a burst of navigations collapses into a single cycle.
    private static readonly ThrottledDebouncedAction ReleaseAction = new(Release, TimeSpan.FromSeconds(5));

    private static InterlockedBoolean _releaseInProgress;

    /// <summary>
    /// Schedules a release after navigating away from a page.
    /// </summary>
    public static void AfterNavigation() => ReleaseAction.Invoke();

    private static void Release()
    {
        // Collect, drain, collect:
        // a bare collect only queues the finalizers, and it is the finalizers that release the cross-process proxies.
        // Don't run on the UI thread - apartment-bound proxies marshal their release back to it.
        // A slow cycle may still be draining; come back later rather than dropping this request.
        if (!_releaseInProgress.Set())
        {
            ReleaseAction.Invoke();
            return;
        }

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            _releaseInProgress.Clear();
        }
    }
}
