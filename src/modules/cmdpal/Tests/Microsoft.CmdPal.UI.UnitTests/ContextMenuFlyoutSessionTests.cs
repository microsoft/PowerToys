// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ContextMenuFlyoutSessionTests
{
    [TestMethod]
    [DataRow(true, false, true, 1)]
    [DataRow(false, false, true, 0)]
    [DataRow(true, true, true, 0)]
    [DataRow(true, false, false, 1)]
    [DataRow(false, false, false, 0)]
    [DataRow(true, true, false, 0)]
    public void Closed_ReleasesOnlyCompletedClose(bool closing, bool isOpen, bool canEnqueue, int expectedReleases)
    {
        var callbacks = new Queue<Action>();
        var releases = 0;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => { }, () => releases++, callback =>
        {
            if (canEnqueue)
            {
                callbacks.Enqueue(callback);
            }

            return canEnqueue;
        });
        if (closing)
        {
            session.Closing();
        }

        session.Closed();
        if (canEnqueue)
        {
            Assert.AreEqual(0, releases, "Release must wait until the turn after Closed.");
            callbacks.Dequeue()();
        }

        Assert.AreEqual(expectedReleases, releases);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RequestDuringClose_WaitsUntilAfterClosed(bool requestAfterClosed)
    {
        var callbacks = new Queue<Action>();
        var session = new ContextMenuFlyoutSession(() => false, () => { }, () => { }, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var rows = "First";
        session.Closing();
        if (requestAfterClosed)
        {
            session.Closed();
        }

        // IsOpen is already false, but WinUI still owns the previous flyout.
        session.Show(() => rows = "Second");
        Assert.AreEqual("First", rows);
        if (!requestAfterClosed)
        {
            Assert.IsEmpty(callbacks);
            session.Closed();
        }

        Assert.AreEqual("First", rows);
        callbacks.Dequeue()();
        Assert.AreEqual("Second", rows);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Reopen_IsDeferredUntilClosedReturns(bool stillReportsOpen)
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var releases = 0;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => isOpen = stillReportsOpen, () => releases++, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var rows = "First";
        session.Show(() => rows = "Second");
        Assert.AreEqual("First", rows);

        session.Closing();
        session.Closed();
        Assert.AreEqual("First", rows, "WinUI must finish its Closed handler before the replacement is prepared.");
        Assert.AreEqual(0, releases);
        callbacks.Dequeue()();
        Assert.AreEqual("Second", rows, "A stale IsOpen value must not prevent replaying the request.");
        Assert.AreEqual(stillReportsOpen ? 0 : 1, releases);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PendingReopen_UsesLatestRequestOrHonorsCancellation(bool cancel)
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var releases = 0;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => { }, () => releases++, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var opened = string.Empty;
        session.Closing();
        isOpen = false;
        session.Show(() => opened = "Superseded");
        session.Show(() => opened = "Latest");
        if (cancel)
        {
            session.Hide();
        }

        Assert.AreEqual(string.Empty, opened);
        session.Closed();
        callbacks.Dequeue()();
        Assert.AreEqual(cancel ? string.Empty : "Latest", opened);
        Assert.AreEqual(1, releases);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    public void PendingReopen_WhenRequestBecomesInvalid_ReleasesPreviousContext()
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var canOpen = true;
        var retainedContext = true;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => isOpen = false, () => retainedContext = false, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        session.Show(() =>
        {
            if (!canOpen)
            {
                return;
            }

            isOpen = true;
        });

        session.Closing();
        canOpen = false;
        session.Closed();
        Assert.IsTrue(retainedContext, "Release must wait until the turn after Closed.");
        callbacks.Dequeue()();
        Assert.IsFalse(isOpen);
        Assert.IsFalse(retainedContext, "Rejecting a queued reopen must still release the previous menu.");
        Assert.IsEmpty(callbacks);

        session.Show(() => isOpen = true);
        Assert.IsTrue(isOpen, "Rejecting a queued reopen must not block subsequent requests.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NewRequestBeforeQueuedCallback_RunsOnlyLatest(bool stillReportsOpen)
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => isOpen = false, () => { }, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var opened = string.Empty;
        var openCount = 0;
        session.Show(() => opened = "Superseded");
        session.Closing();
        session.Closed();
        isOpen = stillReportsOpen;
        session.Show(() =>
        {
            opened = "Latest";
            openCount++;
        });

        Assert.AreEqual(0, openCount);
        callbacks.Dequeue()();
        Assert.AreEqual("Latest", opened);
        Assert.AreEqual(1, openCount);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    public void LateOrDuplicateClosed_DoesNotReplayOrClearReopenedMenu()
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => isOpen = false, () => Assert.Fail("A late Closed must not clear the reopened menu."), callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var rows = "First";
        var openCount = 0;
        session.Show(() =>
        {
            rows = "Second";
            isOpen = true;
            openCount++;
        });

        session.Closed();
        session.Closed();
        callbacks.Dequeue()();
        callbacks.Dequeue()();
        session.Closed();
        callbacks.Dequeue()();
        Assert.AreEqual("Second", rows);
        Assert.AreEqual(1, openCount);
        Assert.IsTrue(isOpen);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    public void ClosedWithoutClosing_ReplaysPendingRequest()
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var session = new ContextMenuFlyoutSession(() => isOpen, () => isOpen = false, () => Assert.Fail("Closed without Closing must not release the menu."), callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        var opened = string.Empty;
        session.Show(() => opened = "Current");
        session.Closed();
        Assert.AreEqual(string.Empty, opened);
        callbacks.Dequeue()();
        Assert.AreEqual("Current", opened);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedOrCancelledOpen_DoesNotBlockNextRequest(bool throws)
    {
        var callbacks = new Queue<Action>();
        var session = new ContextMenuFlyoutSession(() => false, () => { }, () => { }, callback =>
        {
            callbacks.Enqueue(callback);
            return true;
        });
        if (throws)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Show(() => throw new InvalidOperationException()));
        }
        else
        {
            session.Show(() => { });
        }

        session.Closed();
        var opened = false;
        session.Show(() => opened = true);
        callbacks.Dequeue()();
        Assert.IsTrue(opened);
        Assert.IsEmpty(callbacks);
    }

    [TestMethod]
    public void UnavailableDispatcher_DropsPendingRequestAndResetsClosing()
    {
        var releases = 0;
        var session = new ContextMenuFlyoutSession(() => false, () => { }, () => releases++, _ => false);
        var opened = string.Empty;
        session.Closing();
        session.Show(() => opened = "Abandoned");
        Assert.AreEqual(string.Empty, opened);
        session.Closed();
        Assert.AreEqual(1, releases);

        session.Show(() => opened = "Current");
        Assert.AreEqual("Current", opened);
    }

    [TestMethod]
    public void Close_WhenPopupAlreadyClosedCancelsDeferredRequest()
    {
        var callbacks = new Queue<Action>();
        var isOpen = true;
        var hideCount = 0;
        var session = new ContextMenuFlyoutSession(
            () => isOpen,
            () =>
            {
                isOpen = false;
                hideCount++;
            },
            () => { },
            callback =>
            {
                callbacks.Enqueue(callback);
                return true;
            });
        var opened = false;
        session.Show(() => opened = true);
        session.Closing();
        session.Closed();
        session.Hide();
        callbacks.Dequeue()();
        Assert.IsFalse(opened);
        Assert.AreEqual(1, hideCount);
        Assert.IsEmpty(callbacks);
    }
}
