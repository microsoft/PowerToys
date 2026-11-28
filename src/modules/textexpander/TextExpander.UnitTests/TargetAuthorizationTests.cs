// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class TargetAuthorizationTests
{
    [TestMethod]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public void AnOlderResultCannotReplaceTheLatestRequest(bool sameWindow, bool latestAllowed)
    {
        // Pause the old worker after its pending/foreground checks, in its final PID check. The new request finishes
        // before it resumes: checking a generation before this point cannot make the final publication atomic.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("btw=By the way"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var oldReady = new ManualResetEventSlim();
        using var resumeOld = new ManualResetEventSlim();
        IntPtr oldWindow = new(10);
        IntPtr currentWindow = sameWindow ? oldWindow : new IntPtr(11);
        int requests = 0;
        KeyboardHook? hook = null;
        hook = new KeyboardHook(store)
        {
            RequestAuthorization = window =>
            {
                requests++;
                hook!.RefreshTargetAuthorization(window, asynchronous: false, _ => 20, (_, _) => latestAllowed, () => currentWindow);
            },
            CaptureForeground = () => hook!.CaptureForWindow(currentWindow, 20),
            InPasswordBox = static () => false,
            UserInput = static () => { },
        };

        using (hook)
        {
            int lookups = 0;
            Task old = Task.Run(() => hook.RefreshTargetAuthorization(
                oldWindow,
                asynchronous: false,
                _ =>
                {
                    if (++lookups == 2)
                    {
                        oldReady.Set();
                        Assert.IsTrue(resumeOld.Wait(10_000));
                    }

                    return 20;
                },
                (_, _) => true,
                () => oldWindow));

            try
            {
                Assert.IsTrue(oldReady.Wait(10_000));
                Assert.AreEqual(KeyboardHook.ForegroundCapture.Pending, hook.CaptureForWindow(currentWindow, 20));
                Assert.AreEqual(1, requests);
                KeyboardHook.ForegroundCapture expected = latestAllowed ? KeyboardHook.ForegroundCapture.Authorized : KeyboardHook.ForegroundCapture.Refused;
                Assert.AreEqual(expected, hook.CaptureForWindow(currentWindow, 20));
                resumeOld.Set();
                Assert.IsTrue(old.Wait(10_000));
                Assert.AreEqual(expected, hook.CaptureForWindow(currentWindow, 20));
                Assert.AreEqual(expected, hook.CaptureForWindow(currentWindow, 20));
                Assert.AreEqual(1, requests, "A completed check, including a refusal, is not requested again on each key.");

                bool wordMode = WordBoundary.Required;
                try
                {
                    WordBoundary.Required = true;
                    bool matched = false;
                    foreach (char ch in "btw ")
                    {
                        matched = hook.OnKeyEvent(FakeTextBox.UserKey(ch, up: false), Native.WM_KEYDOWN);
                    }

                    Assert.AreEqual(latestAllowed, matched, "Capture and matching use the latest completed authorization.");
                    Assert.AreEqual(latestAllowed ? 1 : 0, hook.Pending.Count);
                }
                finally
                {
                    WordBoundary.Required = wordMode;
                }
            }
            finally
            {
                resumeOld.Set();
                Assert.IsTrue(old.Wait(10_000));
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnOlderResultCannotFinishANewerPendingRequest(bool latestAllowed)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        using var store = new SnippetStore(Path.Combine(scratch.Path, "snippets.json"));
        using var oldReady = new ManualResetEventSlim();
        using var resumeOld = new ManualResetEventSlim();
        using var latestReady = new ManualResetEventSlim();
        using var finishLatest = new ManualResetEventSlim();
        IntPtr window = new(10);
        int requests = 0;
        Task? latest = null;
        KeyboardHook? hook = null;
        hook = new KeyboardHook(store)
        {
            RequestAuthorization = target =>
            {
                requests++;
                latest = Task.Run(() => hook!.RefreshTargetAuthorization(
                    target,
                    asynchronous: false,
                    _ => 20,
                    (_, _) =>
                    {
                        latestReady.Set();
                        Assert.IsTrue(finishLatest.Wait(10_000));
                        return latestAllowed;
                    },
                    () => window));
            },
        };

        using (hook)
        {
            int lookups = 0;
            Task old = Task.Run(() => hook.RefreshTargetAuthorization(
                window,
                asynchronous: false,
                _ =>
                {
                    if (++lookups == 2)
                    {
                        oldReady.Set();
                        Assert.IsTrue(resumeOld.Wait(10_000));
                    }

                    return 20;
                },
                (_, _) => true,
                () => window));

            try
            {
                Assert.IsTrue(oldReady.Wait(10_000));
                Assert.AreEqual(KeyboardHook.ForegroundCapture.Pending, hook.CaptureForWindow(window, 20));
                Assert.IsTrue(latestReady.Wait(10_000));
                resumeOld.Set();
                Assert.IsTrue(old.Wait(10_000));
                Assert.AreEqual(KeyboardHook.ForegroundCapture.Pending, hook.CaptureForWindow(window, 20), "Only the newer check may finish its pending request.");
                Assert.AreEqual(1, requests, "The pending request is not restarted after the older result returns.");

                finishLatest.Set();
                Assert.IsNotNull(latest);
                Assert.IsTrue(latest.Wait(10_000));
                KeyboardHook.ForegroundCapture expected = latestAllowed ? KeyboardHook.ForegroundCapture.Authorized : KeyboardHook.ForegroundCapture.Refused;
                Assert.AreEqual(expected, hook.CaptureForWindow(window, 20));
                Assert.AreEqual(1, requests);
            }
            finally
            {
                resumeOld.Set();
                finishLatest.Set();
                Assert.IsTrue(old.Wait(10_000));
                Assert.IsTrue(latest is not null && latest.Wait(10_000));
            }
        }
    }
}
