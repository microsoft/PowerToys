// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Helpers for asserting that a view-model is reachable only through the
/// references a test deliberately holds.
/// </summary>
internal static class GcAssert
{
    // BatchUpdateManager parks queued targets in a static queue until its 40ms
    // timer drains them, so a view-model that raised a property change stays
    // rooted regardless of cleanup. Wait that out before measuring, or every
    // assertion here fails for the wrong reason.
    private const int BatchDrainMilliseconds = 200;

    public static void IsCollected<T>(WeakReference<T> reference, string what)
        where T : class
    {
        Thread.Sleep(BatchDrainMilliseconds);
        ForceFullCollection();

        Assert.IsFalse(
            reference.TryGetTarget(out _),
            $"{what} is still reachable after cleanup.");
    }

    public static void IsAlive<T>(WeakReference<T> reference, string what)
        where T : class
    {
        Thread.Sleep(BatchDrainMilliseconds);
        ForceFullCollection();

        Assert.IsTrue(
            reference.TryGetTarget(out _),
            $"{what} was collected while the test still held it.");
    }

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
