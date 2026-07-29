// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the atomic provider-id reservation registry (r2-p4-01). A provider id can be
/// owned by exactly one directory at a time regardless of how many registration paths
/// race for it (full scan, hot reload, or concurrent install), and it is freed only by
/// its owner so a stale release cannot steal an id that was reclaimed by someone else.
/// </summary>
[TestClass]
public class ProviderIdReservationsTests
{
    [TestMethod]
    public void TryReserve_FreeId_Succeeds()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));
    }

    [TestMethod]
    public void TryReserve_SameDirectoryAgain_IsIdempotent()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));

        // The same owner re-reserving its own id must succeed (a hot-reload re-registers
        // the same directory). Callers pass canonical keys, which compare case-insensitively
        // to match the rest of the service's directory comparisons.
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));
        Assert.IsTrue(reservations.TryReserve("alpha", @"c:\ext\A"));
    }

    [TestMethod]
    public void TryReserve_DifferentDirectorySameId_Fails()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));
        Assert.IsFalse(reservations.TryReserve("alpha", @"C:\ext\b"));
    }

    [TestMethod]
    public void TryReserve_EmptyId_AlwaysSucceedsAndReservesNothing()
    {
        var reservations = new ProviderIdReservations();

        // An extension with no name key has nothing to collide on; two different
        // directories can both "reserve" an empty id.
        Assert.IsTrue(reservations.TryReserve(string.Empty, @"C:\ext\a"));
        Assert.IsTrue(reservations.TryReserve(string.Empty, @"C:\ext\b"));
        Assert.IsTrue(reservations.TryReserve(null, @"C:\ext\c"));
    }

    [TestMethod]
    public void Release_ByOwner_FreesId()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));

        reservations.Release("alpha", @"C:\ext\a");

        // Now a different directory may claim it.
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\b"));
    }

    [TestMethod]
    public void Release_ByNonOwner_DoesNotFreeId()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));

        // A stale release from a directory that does not own the id must be ignored, so
        // the real owner keeps it.
        reservations.Release("alpha", @"C:\ext\b");

        Assert.IsFalse(reservations.TryReserve("alpha", @"C:\ext\b"));
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));
    }

    [TestMethod]
    public void Clear_ReleasesEverything()
    {
        var reservations = new ProviderIdReservations();
        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\a"));
        Assert.IsTrue(reservations.TryReserve("beta", @"C:\ext\b"));

        reservations.Clear();

        Assert.IsTrue(reservations.TryReserve("alpha", @"C:\ext\x"));
        Assert.IsTrue(reservations.TryReserve("beta", @"C:\ext\y"));
    }

    [TestMethod]
    public async Task TryReserve_ConcurrentDifferentDirectories_ExactlyOneWins()
    {
        var reservations = new ProviderIdReservations();
        const int Contenders = 32;

        using var start = new ManualResetEventSlim(false);
        var winners = new ConcurrentBag<int>();
        var tasks = new Task[Contenders];

        for (var i = 0; i < Contenders; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                // Every contender blocks on the same gate so they all race the reservation
                // at once, simulating a full scan, a hot reload, and a concurrent install
                // all claiming the same id.
                start.Wait();
                if (reservations.TryReserve("shared-id", $@"C:\ext\dir-{index}"))
                {
                    winners.Add(index);
                }
            });
        }

        start.Set();
        await Task.WhenAll(tasks);

        Assert.AreEqual(1, winners.Count, "Exactly one directory may claim a provider id, regardless of the registration path.");
    }
}
