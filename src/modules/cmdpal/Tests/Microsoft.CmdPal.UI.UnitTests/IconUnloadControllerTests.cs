// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconUnloadControllerTests
{
    [TestMethod]
    public void RecycledUnloadPreservesTheVisibleRowsRequestAndDemand()
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        var requestVersion = host.RequestVersion;

        host.Controller.Unloaded();

        Assert.AreEqual(requestVersion, host.RequestVersion);
        Assert.IsTrue(host.Demand.IsDemanded);
        Assert.HasCount(1, host.Callbacks);

        host.Callbacks.Dequeue()();

        Assert.AreEqual(0, host.CleanupCount);
        Assert.AreEqual(requestVersion, host.RequestVersion, "The completed icon must still match the current request.");
        Assert.IsTrue(host.Demand.IsDemanded);
    }

    [TestMethod]
    [DataRow(true, false, 1)]
    [DataRow(false, true, 0)]
    public void CleanupUsesLoadedStateAfterTheNotificationReturns(bool loadedAtNotification, bool loadedAtDispatch, int expectedCleanup)
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        host.IsLoaded = loadedAtNotification;

        host.Controller.Unloaded();

        Assert.AreEqual(0, host.CleanupCount);
        host.IsLoaded = loadedAtDispatch;
        host.Callbacks.Dequeue()();

        Assert.AreEqual(expectedCleanup, host.CleanupCount);
        Assert.AreEqual(expectedCleanup == 0, host.Demand.IsDemanded);
    }

    [TestMethod]
    public void ReloadCancelsCleanupForThePreviousAttachment()
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        host.Controller.Unloaded();

        host.ReplaceRequest();
        host.Controller.Loaded();
        host.IsLoaded = false;
        host.Callbacks.Dequeue()();

        Assert.AreEqual(0, host.CleanupCount, "An earlier unload must not clean up the replacement attachment.");
        Assert.IsTrue(host.Demand.IsDemanded);
    }

    [TestMethod]
    public void ReloadThenUnloadUsesOneCallbackForTheCurrentAttachment()
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        host.Controller.Unloaded();

        host.ReplaceRequest();
        host.Controller.Loaded();
        host.Controller.Unloaded();
        host.IsLoaded = false;

        Assert.HasCount(1, host.Callbacks);

        host.Callbacks.Dequeue()();
        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
    }

    [TestMethod]
    public void RepeatedUnloadsCoalesceAndReleaseDemandOnce()
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        host.Controller.Unloaded();
        host.Controller.Unloaded();

        Assert.HasCount(1, host.Callbacks);
        host.IsLoaded = false;
        var callback = host.Callbacks.Dequeue();
        callback();
        callback();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
    }

    [TestMethod]
    public void IgnoredUnloadDoesNotPreventLaterDetachmentCleanup()
    {
        var host = new TestIconHost();
        host.Controller.Loaded();
        host.Controller.Unloaded();
        host.Callbacks.Dequeue()();

        host.Controller.Unloaded();
        host.IsLoaded = false;
        host.Callbacks.Dequeue()();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
    }

    [TestMethod]
    public void DispatcherShutdownReleasesDemandWithoutAQueuedCallback()
    {
        var host = new TestIconHost(canEnqueue: false);
        host.Controller.Loaded();

        host.Controller.Unloaded();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.IsEmpty(host.Callbacks);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CleanupFailureIsContainedAndDoesNotBlockLaterUnloads(bool canEnqueue)
    {
        var host = new TestIconHost(canEnqueue)
        {
            IsLoaded = false,
            CleanupException = new InvalidOperationException("Cleanup failed after releasing demand."),
        };
        host.Controller.Loaded();

        host.Controller.Unloaded();
        host.DrainCallbacks();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.HasCount(1, host.Failures);
        Assert.AreSame(host.CleanupException, host.Failures[0]);

        host.ReplaceRequest();
        host.Controller.Loaded();
        host.Controller.Unloaded();
        host.DrainCallbacks();

        Assert.AreEqual(2, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.HasCount(2, host.Failures);
    }

    [TestMethod]
    public void ThrowingEnqueueFallsBackAndCanQueueALaterUnload()
    {
        var host = new TestIconHost
        {
            EnqueueException = new InvalidOperationException("The queue is unavailable."),
        };
        host.Controller.Loaded();

        host.Controller.Unloaded();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.IsEmpty(host.Callbacks);
        Assert.HasCount(1, host.Failures);

        host.ReplaceRequest();
        host.Controller.Loaded();
        host.EnqueueException = null;
        host.IsLoaded = false;
        host.Controller.Unloaded();
        host.DrainCallbacks();

        Assert.AreEqual(2, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.HasCount(1, host.Failures);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FailureReporterCannotEscapeAnUnloadCallback(bool canEnqueue)
    {
        var host = new TestIconHost(canEnqueue)
        {
            IsLoaded = false,
            CleanupException = new InvalidOperationException("Cleanup failed."),
            FailureReporterException = new InvalidOperationException("Logging failed."),
        };
        host.Controller.Loaded();
        host.Controller.Unloaded();

        host.DrainCallbacks();

        Assert.AreEqual(1, host.CleanupCount);
        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.HasCount(1, host.Failures);
    }

    [TestMethod]
    public void ThrowingDemandObserverCannotEscapeTheUnloadCallback()
    {
        var host = new TestIconHost { IsLoaded = false };
        var observer = new ThrowingDemandObserver();
        host.Demand.Subscribe(observer);
        host.Controller.Loaded();
        host.Controller.Unloaded();

        host.DrainCallbacks();

        Assert.IsFalse(host.Demand.IsDemanded);
        Assert.HasCount(1, host.Failures);
        host.Demand.Unsubscribe(observer);
    }

    private sealed class ThrowingDemandObserver : IconLoadDemand.IObserver
    {
        public void OnDemandChanged() => throw new InvalidOperationException("Demand observer failed.");
    }

    private sealed class TestIconHost
    {
        private IconRequestDemandState _request;

        public bool IsLoaded { get; set; } = true;

        public int RequestVersion { get; private set; } = 4;

        public int CleanupCount { get; private set; }

        public Exception? EnqueueException { get; set; }

        public Exception? CleanupException { get; set; }

        public Exception? FailureReporterException { get; set; }

        public List<Exception> Failures { get; } = new();

        public Queue<Action> Callbacks { get; } = new();

        public IconLoadDemand Demand { get; } = new();

        public IconUnloadController Controller { get; }

        public TestIconHost(bool canEnqueue = true)
        {
            _request.Attach(Demand);
            Controller = new(
                () => IsLoaded,
                Cleanup,
                () => TryEnqueue(canEnqueue),
                exception =>
                {
                    Failures.Add(exception);
                    if (FailureReporterException is not null)
                    {
                        throw FailureReporterException;
                    }
                });
        }

        public void DrainCallbacks()
        {
            while (Callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }

        public void ReplaceRequest()
        {
            _request.Release();
            _request = default;
            _request.Attach(Demand);
            ++RequestVersion;
        }

        private bool TryEnqueue(bool canEnqueue)
        {
            if (EnqueueException is not null)
            {
                throw EnqueueException;
            }

            if (canEnqueue)
            {
                Callbacks.Enqueue(Controller.ProcessUnload);
            }

            return canEnqueue;
        }

        private void Cleanup()
        {
            ++CleanupCount;
            ++RequestVersion;
            _request.Release();
            if (CleanupException is not null)
            {
                throw CleanupException;
            }
        }
    }
}
