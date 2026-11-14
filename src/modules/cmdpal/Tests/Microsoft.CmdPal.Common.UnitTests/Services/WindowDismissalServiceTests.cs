// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Common.UnitTests.Services;

[TestClass]
public class WindowDismissalServiceTests
{
    [TestMethod]
    public void OverlappingHoldsAreIndependentAndScopedToTheirOwner()
    {
        var service = new WindowDismissalService();
        var releasedOwners = new List<nint>();
        service.LastHoldReleased += (_, owner) => releasedOwners.Add(owner);
        using var first = service.Acquire(101);
        using var second = service.Acquire(101);
        using var otherWindow = service.Acquire(202);

        first.Dispose();
        first.Dispose();
        Assert.IsTrue(service.IsSuppressed(101));
        Assert.IsTrue(service.IsSuppressed(202));
        Assert.AreEqual(0, releasedOwners.Count);

        second.Dispose();
        Assert.IsFalse(service.IsSuppressed(101));
        Assert.IsTrue(service.IsSuppressed(202));
        CollectionAssert.AreEqual(new nint[] { 101 }, releasedOwners);

        otherWindow.Dispose();
        Assert.IsFalse(service.IsSuppressed(202));
        CollectionAssert.AreEqual(new nint[] { 101, 202 }, releasedOwners);
    }

    [TestMethod]
    public void ClosingSessionInvalidatesOldHoldsWithoutAffectingNewOnes()
    {
        var service = new WindowDismissalService();
        var releasedOwners = new List<nint>();
        service.LastHoldReleased += (_, owner) => releasedOwners.Add(owner);
        using var oldHold = service.Acquire(101);
        using var otherWindow = service.Acquire(202);
        service.Reset(101);
        Assert.IsFalse(service.IsSuppressed(101));
        Assert.IsTrue(service.IsSuppressed(202));

        using var newHold = service.Acquire(101);
        oldHold.Dispose();
        Assert.IsTrue(service.IsSuppressed(101));
        Assert.AreEqual(0, releasedOwners.Count);
        newHold.Dispose();
        Assert.IsFalse(service.IsSuppressed(101));
        CollectionAssert.AreEqual(new nint[] { 101 }, releasedOwners);
    }

    [TestMethod]
    public void FinalReleaseNotifiesOutsideTheLock()
    {
        var service = new WindowDismissalService();
        var readCompleted = false;
        var wasSuppressed = true;
        service.LastHoldReleased += (_, owner) =>
        {
            var read = Task.Run(() => service.IsSuppressed(owner));
            readCompleted = read.Wait(TimeSpan.FromSeconds(5));
            if (readCompleted)
            {
                wasSuppressed = read.Result;
            }
        };

        service.Acquire(101).Dispose();
        Assert.IsTrue(readCompleted, "Release callbacks must not hold the service lock.");
        Assert.IsFalse(wasSuppressed);
    }

    [TestMethod]
    public void ConcurrentDisposalDoesNotReleaseAnotherHold()
    {
        var service = new WindowDismissalService();
        using var first = service.Acquire(101);
        using var second = service.Acquire(101);
        Parallel.For(0, 100, _ => first.Dispose());
        Assert.IsTrue(service.IsSuppressed(101));
        second.Dispose();
        Assert.IsFalse(service.IsSuppressed(101));
    }

    [TestMethod]
    public void MissingOwnerCannotAcquireHold()
    {
        var service = new WindowDismissalService();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => service.Acquire(0));
        Assert.IsFalse(service.IsSuppressed(0));
    }
}
