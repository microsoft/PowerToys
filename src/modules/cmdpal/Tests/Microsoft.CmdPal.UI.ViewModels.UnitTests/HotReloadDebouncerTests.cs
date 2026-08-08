// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class HotReloadDebouncerTests
{
    [TestMethod]
    public void IsRelevantChange_NodeModulesPath_IsIgnored()
    {
        Assert.IsFalse(HotReloadDebouncer.IsRelevantChange(@"C:\ext\node_modules\pkg\index.js"));
        Assert.IsFalse(HotReloadDebouncer.IsRelevantChange(@"C:\ext\NODE_MODULES\pkg\index.js"));
        Assert.IsFalse(HotReloadDebouncer.IsRelevantChange(string.Empty));
    }

    [TestMethod]
    public void IsRelevantChange_SourceFile_IsRelevant()
    {
        Assert.IsTrue(HotReloadDebouncer.IsRelevantChange(@"C:\ext\dist\index.js"));
    }

    [TestMethod]
    public void Notify_RapidChanges_InvokesCallbackOnce()
    {
        var fired = new CountdownEvent(1);
        var count = 0;
        using var debouncer = new HotReloadDebouncer(
            _ =>
            {
                Interlocked.Increment(ref count);
                fired.Signal();
            },
            TimeSpan.FromMilliseconds(120));

        // Simulate a burst of saves well within the debounce window.
        for (var i = 0; i < 8; i++)
        {
            debouncer.Notify(@"C:\ext", @"C:\ext\dist\index.js");
            Thread.Sleep(10);
        }

        Assert.IsTrue(fired.Wait(TimeSpan.FromSeconds(2)), "Callback was not invoked.");

        // Give any (erroneous) extra timers a chance to fire before asserting.
        Thread.Sleep(200);
        Assert.AreEqual(1, Volatile.Read(ref count));
    }

    [TestMethod]
    public void Notify_NodeModulesChange_DoesNotInvokeCallback()
    {
        var count = 0;
        using var debouncer = new HotReloadDebouncer(
            _ => Interlocked.Increment(ref count),
            TimeSpan.FromMilliseconds(80));

        debouncer.Notify(@"C:\ext", @"C:\ext\node_modules\pkg\index.js");

        Thread.Sleep(250);
        Assert.AreEqual(0, Volatile.Read(ref count));
    }

    [TestMethod]
    public void Notify_DistinctKeys_InvokesCallbackPerKey()
    {
        var fired = new CountdownEvent(2);
        using var debouncer = new HotReloadDebouncer(
            _ => fired.Signal(),
            TimeSpan.FromMilliseconds(80));

        debouncer.Notify(@"C:\ext-a", @"C:\ext-a\index.js");
        debouncer.Notify(@"C:\ext-b", @"C:\ext-b\index.js");

        Assert.IsTrue(fired.Wait(TimeSpan.FromSeconds(2)), "Both keys should have fired.");
    }

    [TestMethod]
    public void Cancel_PreventsPendingCallback()
    {
        var count = 0;
        using var debouncer = new HotReloadDebouncer(
            _ => Interlocked.Increment(ref count),
            TimeSpan.FromMilliseconds(150));

        debouncer.Notify(@"C:\ext", @"C:\ext\index.js");
        debouncer.Cancel(@"C:\ext");

        Thread.Sleep(300);
        Assert.AreEqual(0, Volatile.Read(ref count));
    }

    // r3-p4-09: a debounce pending when the service stops between load generations must
    // not fire against the next generation. CancelAll advances the generation and cancels
    // every pending timer, so a callback armed before the stop is dropped.
    [TestMethod]
    public void CancelAll_DropsPendingCallbacksAcrossKeys()
    {
        var count = 0;
        using var debouncer = new HotReloadDebouncer(
            _ => Interlocked.Increment(ref count),
            TimeSpan.FromMilliseconds(150));

        debouncer.Notify(@"C:\ext-a", @"C:\ext-a\index.js");
        debouncer.Notify(@"C:\ext-b", @"C:\ext-b\index.js");
        debouncer.CancelAll();

        Thread.Sleep(350);
        Assert.AreEqual(0, Volatile.Read(ref count), "Pending callbacks must not fire after CancelAll.");
    }

    // r3-p4-09: after a stop (CancelAll), a fresh Notify belongs to the new generation and
    // must still fire normally, proving CancelAll cancels the prior generation only.
    [TestMethod]
    public void CancelAll_DoesNotBlockLaterGeneration()
    {
        var fired = new CountdownEvent(1);
        using var debouncer = new HotReloadDebouncer(
            _ => fired.Signal(),
            TimeSpan.FromMilliseconds(100));

        debouncer.Notify(@"C:\ext", @"C:\ext\index.js");
        debouncer.CancelAll();

        // A change notified after the stop starts a new generation and must fire.
        debouncer.Notify(@"C:\ext", @"C:\ext\index.js");

        Assert.IsTrue(fired.Wait(TimeSpan.FromSeconds(2)), "A change after CancelAll should still reload.");
    }
}
