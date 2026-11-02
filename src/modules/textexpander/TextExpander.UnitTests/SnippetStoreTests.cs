// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetStoreTests
{
    private static readonly string[] StarterTriggers = ["!ddate", "!ttime", "!ddt", "!isodate", "!mysig", "!myemail"];
    private static readonly byte[] WinnerBytes = SnippetJson.Bytes("!external=winner");

    [TestMethod]
    public void ConfiguredPathIsTheFileOrTheHostDefault()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();

        // A configured path is the snippets file, whatever its name, used where it is: its folder isn't created.
        string chosen = Path.Combine(scratch.Path, "library", "work.json");
        Assert.AreEqual(new SnippetPathResolution(chosen, IsDefault: false), SnippetStore.ResolveConfiguredPathInfo(chosen, null));
        Assert.IsFalse(Directory.Exists(Path.Combine(scratch.Path, "library")));
        string notes = Write(scratch, "notes.txt");
        Assert.AreEqual(notes, SnippetStore.ResolveConfiguredPathInfo(notes, null).FilePath);

        // Without one, the file is in the host's folder, which is created.
        string host = Path.Combine(scratch.Path, "host");
        var hostDefault = new SnippetPathResolution(Path.Combine(host, "PowerToysTextExpanderSnippets.json"), IsDefault: true);
        Assert.AreEqual(hostDefault, SnippetStore.ResolveConfiguredPathInfo(null, host));
        Assert.AreEqual(hostDefault, SnippetStore.ResolveConfiguredPathInfo("relative.json", host));
        Assert.IsTrue(Directory.Exists(host));
    }

    [TestMethod]
    public void InitialLoadFailureIsExplicitAndRetriesTheDesiredLibrary()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!ready=eventually");

        using var reloaded = new ManualResetEventSlim(false);
        int failures = 1;
        byte[] ReadBytes(string file)
        {
            if (Interlocked.Exchange(ref failures, 0) != 0)
            {
                throw new IOException("Injected initial read failure.");
            }

            return File.ReadAllBytes(file);
        }

        using var store = NewStore(path, ReadBytes);
        store.Reloaded += _ => reloaded.Set();

        SnippetStore.TransitionResult result = store.Load();

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.AreEqual(0, result.Count, "The previous empty count is data, not a success signal.");
        Assert.IsInstanceOfType<IOException>(result.Error);
        Assert.AreEqual(path, result.AttemptedPath);
        Assert.IsFalse(result.HasCurrentState);
        Assert.IsFalse(store.HasLoadedState);
        Assert.AreEqual(0, store.Current.Map.Count);

        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The desired initial library was not retried.");
        Assert.IsTrue(store.HasLoadedState);
        Assert.IsTrue(store.Current.Map.ContainsKey("!ready"));
    }

    [TestMethod]
    public void FailedRepointIsExplicitAndRetriesTheDesiredPath()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "!first=old");
        string second = Write(scratch, "second.json", "!second=new");

        using var reloaded = new ManualResetEventSlim(false);
        int obstructed = 1;
        byte[] ReadBytes(string path)
        {
            if (string.Equals(path, second, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref obstructed, 0) != 0)
            {
                throw new IOException("Injected repoint failure.");
            }

            return File.ReadAllBytes(path);
        }

        using var store = NewStore(first, ReadBytes);
        Assert.IsTrue(store.Load().Succeeded);
        store.Reloaded += _ => reloaded.Set();

        SnippetStore.TransitionResult result = store.Repoint(second);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(second, result.AttemptedPath);
        Assert.AreEqual(first, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!first"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!second"));

        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The failed repoint target was not retained for retry.");
        Assert.AreEqual(second, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!second"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!first"));
    }

    [TestMethod]
    public void FailedCaseChangeIsExplicitAndRetriesTheDesiredComparison()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "Key=first", "key=second");

        using var retryStarted = new ManualResetEventSlim(false);
        using var releaseRetry = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        int reads = 0;
        byte[] ReadBytes(string file)
        {
            int read = Interlocked.Increment(ref reads);
            if (read == 2)
            {
                throw new IOException("Injected case-change failure.");
            }

            if (read > 2)
            {
                retryStarted.Set();
                if (!releaseRetry.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The case-change retry gate timed out.");
                }
            }

            return File.ReadAllBytes(file);
        }

        using var store = NewStore(path, ReadBytes);
        Assert.IsTrue(store.Load().Succeeded);
        store.Reloaded += _ => reloaded.Set();

        SnippetStore.TransitionResult result = store.SetCaseSensitive(caseSensitive: false);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(2, store.Current.Map.Count);
        Assert.IsTrue(retryStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2, store.Current.Map.Count, "The old case-sensitive state stays live until the retry commits.");

        releaseRetry.Set();
        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, store.Current.Map.Count);
        Assert.IsTrue(store.Current.Map.TryGetValue("KEY", out string? replacement));
        Assert.AreEqual("second", replacement);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void QueuedOldWatcherReloadCannotUndoANewerRepointOrCaseTransition(bool repoint)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "Key=first", "key=second");
        string second = Write(scratch, "second.json", "!second=new");

        using var captured = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var completed = new ManualResetEventSlim(false);
        int gateTimedOut = 0;
        var hooks = new SnippetStore.TestHooks
        {
            BeforeWatcherReload = _ =>
            {
                captured.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    Interlocked.Exchange(ref gateTimedOut, 1);
                }
            },
            AfterWatcherReload = _ => completed.Set(),
        };

        using var store = NewStore(first, hooks: hooks);
        Assert.IsTrue(store.Load().Succeeded);

        store.ScheduleReload();
        Assert.IsTrue(captured.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue((repoint ? store.Repoint(second) : store.SetCaseSensitive(caseSensitive: false)).Succeeded);

        release.Set();
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, Volatile.Read(ref gateTimedOut), "The queued watcher gate timed out.");
        Assert.AreEqual(repoint ? second : first, store.FilePath);
        if (repoint)
        {
            Assert.IsTrue(store.Current.Map.ContainsKey("!second"));
            Assert.IsFalse(store.Current.Map.ContainsKey("Key"));
        }
        else
        {
            Assert.AreEqual(1, store.Current.Map.Count);
            Assert.IsTrue(store.Current.Map.TryGetValue("KEY", out string? replacement));
            Assert.AreEqual("second", replacement);
        }
    }

    [TestMethod]
    public void NewerWatcherEventDuringAnInflightReadAlwaysGetsASecondRead()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();

        for (int iteration = 0; iteration < 8; iteration++)
        {
            int run = iteration;
            string path = Write(scratch, $"sequence-{run}.json", "!initial=zero");
            string[] contents = ["!initial=zero"];
            Action? raiseWatcherEvent = null;
            using var firstReadStarted = new ManualResetEventSlim(false);
            using var releaseFirstRead = new ManualResetEventSlim(false);
            using var secondReadStarted = new ManualResetEventSlim(false);
            using var finalReloaded = new ManualResetEventSlim(false);
            int trackReloads = 0;
            int reloadReads = 0;

            byte[] ReadBytes(string file)
            {
                string[] captured = [.. Volatile.Read(ref contents)];
                if (Volatile.Read(ref trackReloads) != 0)
                {
                    int read = Interlocked.Increment(ref reloadReads);
                    if (read == 1)
                    {
                        firstReadStarted.Set();
                        if (!releaseFirstRead.Wait(TimeSpan.FromSeconds(5)))
                        {
                            throw new IOException("The first sequenced reload gate timed out.");
                        }
                    }
                    else if (read == 2)
                    {
                        secondReadStarted.Set();
                    }
                }

                return SnippetJson.Bytes(captured);
            }

            var hooks = new SnippetStore.TestHooks
            {
                WatcherFileEventHandlerRegistered = (_, handler) =>
                    Interlocked.CompareExchange(ref raiseWatcherEvent, handler, null),
            };

            using var store = NewStore(path, ReadBytes, hooks);
            Assert.IsTrue(store.Load().Succeeded);
            Assert.IsTrue(store.StartWatching().Succeeded);
            Assert.IsNotNull(raiseWatcherEvent);
            store.Reloaded += _ =>
            {
                if (store.Current.Map.ContainsKey($"!second{run}"))
                {
                    finalReloaded.Set();
                }
            };

            Volatile.Write(ref trackReloads, 1);
            Volatile.Write(ref contents, [$"!first{run}=one"]);
            raiseWatcherEvent!();
            Assert.IsTrue(firstReadStarted.Wait(TimeSpan.FromSeconds(5)), $"Run {run}: the first reload did not start.");

            Volatile.Write(ref contents, [$"!second{run}=two"]);
            raiseWatcherEvent();
            releaseFirstRead.Set();

            Assert.IsTrue(secondReadStarted.Wait(TimeSpan.FromSeconds(5)), $"Run {run}: the newer event was not reread.");
            Assert.IsTrue(finalReloaded.Wait(TimeSpan.FromSeconds(5)), $"Run {run}: the newer contents were not published.");
            Assert.AreEqual(2, Volatile.Read(ref reloadReads), $"Run {run}: requests should coalesce to exactly the two required reads.");
            Assert.IsTrue(store.Current.Map.ContainsKey($"!second{run}"));
            Assert.IsFalse(store.Current.Map.ContainsKey($"!first{run}"));
        }
    }

    [TestMethod]
    public void FallbackWatcherEventAfterRepointRetriesExhaustRefreshesAndRestartsDesiredTransition()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "!first=old");
        string second = Write(scratch, "second.json", "!second=new");

        string[] fallbackContents = ["!first=old"];
        string[] desiredContents = ["!second=new"];
        Action? raiseFallbackEvent = null;
        using var firstStop = new ManualResetEventSlim(false);
        using var secondStop = new ManualResetEventSlim(false);
        using var fallbackReloaded = new ManualResetEventSlim(false);
        using var desiredReloaded = new ManualResetEventSlim(false);
        int desiredReadable = 0;
        int desiredAttempts = 0;
        int stopCount = 0;

        byte[] ReadBytes(string path)
        {
            if (string.Equals(path, second, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref desiredAttempts);
                if (Volatile.Read(ref desiredReadable) == 0)
                {
                    throw new IOException("Injected unavailable desired library.");
                }

                return SnippetJson.Bytes(Volatile.Read(ref desiredContents));
            }

            return SnippetJson.Bytes(Volatile.Read(ref fallbackContents));
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseFallbackEvent, handler, null),
        };

        using var store = NewStore(first, ReadBytes, hooks, log: message =>
        {
            if (message.Contains("Stopped retrying", StringComparison.Ordinal))
            {
                (Interlocked.Increment(ref stopCount) == 1 ? firstStop : secondStop).Set();
            }
        });
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(raiseFallbackEvent);
        store.Reloaded += _ =>
        {
            if (store.Current.Map.ContainsKey("!fallback"))
            {
                fallbackReloaded.Set();
            }

            if (store.Current.Map.ContainsKey("!second"))
            {
                desiredReloaded.Set();
            }
        };

        Assert.IsFalse(store.Repoint(second).Succeeded);
        Assert.IsTrue(firstStop.Wait(TimeSpan.FromSeconds(5)), "The first desired retry cycle did not stop.");
        int attemptsAfterFirstStop = Volatile.Read(ref desiredAttempts);
        Assert.AreEqual(3, attemptsAfterFirstStop);

        Volatile.Write(ref fallbackContents, ["!fallback=updated"]);
        raiseFallbackEvent!();

        Assert.IsTrue(fallbackReloaded.Wait(TimeSpan.FromSeconds(5)), "The published fallback watcher did not refresh its live snapshot.");
        Assert.IsTrue(secondStop.Wait(TimeSpan.FromSeconds(5)), "The fallback event did not restart bounded desired retries.");
        Assert.AreEqual(attemptsAfterFirstStop + 3, Volatile.Read(ref desiredAttempts));
        Assert.AreEqual(first, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!fallback"));

        Volatile.Write(ref desiredReadable, 1);
        raiseFallbackEvent();

        Assert.IsTrue(desiredReloaded.Wait(TimeSpan.FromSeconds(5)), "The fallback watcher did not recover the desired path.");
        Assert.AreEqual(second, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!second"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!fallback"));
    }

    [TestMethod]
    public void FallbackWatcherEventRecoversFailedCaseSensitivityAfterFileBecomesReadable()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "Key=first", "key=second");

        string[] contents = ["Key=first", "key=second"];
        Action? raiseFallbackEvent = null;
        using var stopped = new ManualResetEventSlim(false);
        using var desiredReadStarted = new ManualResetEventSlim(false);
        using var releaseDesiredRead = new ManualResetEventSlim(false);
        using var recovered = new ManualResetEventSlim(false);
        int failReads = 0;
        int recoveryReads = -100;

        byte[] ReadBytes(string file)
        {
            if (Volatile.Read(ref failReads) != 0)
            {
                throw new IOException("Injected unreadable snippet file.");
            }

            string[] captured = [.. Volatile.Read(ref contents)];
            if (Volatile.Read(ref recoveryReads) >= 0 && Interlocked.Increment(ref recoveryReads) == 2)
            {
                desiredReadStarted.Set();
                if (!releaseDesiredRead.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The desired case-sensitivity read gate timed out.");
                }
            }

            return SnippetJson.Bytes(captured);
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseFallbackEvent, handler, null),
        };

        using var store = NewStore(path, ReadBytes, hooks, log: SignalWhenLogged("Stopped retrying", stopped));
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(raiseFallbackEvent);

        Volatile.Write(ref failReads, 1);
        Assert.IsFalse(store.SetCaseSensitive(caseSensitive: false).Succeeded);
        Assert.IsTrue(stopped.Wait(TimeSpan.FromSeconds(5)), "The failed comparison retry cycle did not stop.");

        Volatile.Write(ref contents, ["Key=fallback", "key=desired"]);
        Volatile.Write(ref failReads, 0);
        Interlocked.Exchange(ref recoveryReads, 0);
        store.Reloaded += _ =>
        {
            if (store.Current.Map.Count == 1 && store.Current.Map.TryGetValue("KEY", out string? value) && value == "desired")
            {
                recovered.Set();
            }
        };
        raiseFallbackEvent!();

        Assert.IsTrue(desiredReadStarted.Wait(TimeSpan.FromSeconds(5)), "The fallback reload did not reattempt the desired comparison.");
        Assert.AreEqual(2, store.Current.Map.Count, "The readable file must first refresh under the still-published comparison.");
        Assert.AreEqual("fallback", store.Current.Map["Key"]);
        Assert.AreEqual("desired", store.Current.Map["key"]);

        releaseDesiredRead.Set();
        Assert.IsTrue(recovered.Wait(TimeSpan.FromSeconds(5)), "The desired case-insensitive state was not committed.");
        Assert.AreEqual(1, store.Current.Map.Count);
        Assert.IsTrue(store.Current.Map.TryGetValue("KEY", out string? replacement));
        Assert.AreEqual("desired", replacement);
        Assert.IsTrue(Volatile.Read(ref recoveryReads) >= 3, "Recovery must include fallback, desired-stage, and watcher-handoff reads.");
    }

    [TestMethod]
    public void StalePublishedWatcherEventIsSuppressedAfterNewerTransitionCommits()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "!first=old");
        string second = Write(scratch, "second.json", "!second=new");

        Action? firstWatcherEvent = null;
        int firstReadsAfterCommit = 0;
        int trackFirstReads = 0;
        using var staleCompleted = new ManualResetEventSlim(false);
        byte[] ReadBytes(string path)
        {
            if (Volatile.Read(ref trackFirstReads) != 0
                && string.Equals(path, first, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref firstReadsAfterCommit);
            }

            return File.ReadAllBytes(path);
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref firstWatcherEvent, handler, null),
            AfterWatcherReload = _ => staleCompleted.Set(),
        };

        using var store = NewStore(first, ReadBytes, hooks);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(firstWatcherEvent);
        Assert.IsTrue(store.Repoint(second).Succeeded);
        Assert.AreEqual(second, store.FilePath);

        Volatile.Write(ref trackFirstReads, 1);
        firstWatcherEvent!();

        Assert.IsFalse(staleCompleted.Wait(TimeSpan.FromMilliseconds(200)), "A stale watcher callback must be rejected before scheduling.");
        Assert.AreEqual(0, Volatile.Read(ref firstReadsAfterCommit));
        Assert.AreEqual(second, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!second"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!first"));
    }

    [TestMethod]
    public void WatcherErrorRecoveryIsNotCancelledByCapturedStaleFileCallback()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        Action<Exception>? raiseWatcherError = null;
        Action? staleFileEvent = null;
        using var recovered = new ManualResetEventSlim(false);
        int useNewContents = 0;
        byte[] ReadBytes(string file)
            => SnippetJson.Bytes(Volatile.Read(ref useNewContents) == 0 ? "!old=before" : "!new=after");

        var hooks = new SnippetStore.TestHooks
        {
            WatcherErrorHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseWatcherError, handler, null),
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref staleFileEvent, handler, null),
        };

        using var store = NewStore(path, ReadBytes, hooks, retryDelay: TimeSpan.FromMilliseconds(150));
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(raiseWatcherError);
        Assert.IsNotNull(staleFileEvent);
        store.Reloaded += _ =>
        {
            if (store.Current.Map.ContainsKey("!new") && store.IsWatching)
            {
                recovered.Set();
            }
        };

        Volatile.Write(ref useNewContents, 1);
        raiseWatcherError!(new IOException("Injected watcher error."));
        staleFileEvent!();

        Assert.IsTrue(recovered.Wait(TimeSpan.FromSeconds(5)), "The stale callback replaced the watcher recovery request.");
        Assert.IsTrue(store.IsWatching);
        Assert.IsTrue(store.Current.Map.ContainsKey("!new"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!old"));
    }

    [TestMethod]
    public void StaleWatcherCallbackCannotReplaceDesiredTransitionRetry()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "!first=one");
        string second = Write(scratch, "second.json", "!second=two");
        string third = Write(scratch, "third.json", "!third=three");

        Action? staleFirstWatcherEvent = null;
        using var recovered = new ManualResetEventSlim(false);
        int failThirdRead = 0;
        byte[] ReadBytes(string path)
        {
            if (string.Equals(path, third, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref failThirdRead, 0) != 0)
            {
                throw new IOException("Injected desired transition read failure.");
            }

            return File.ReadAllBytes(path);
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref staleFirstWatcherEvent, handler, null),
        };

        using var store = NewStore(first, ReadBytes, hooks, retryDelay: TimeSpan.FromMilliseconds(150));
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(staleFirstWatcherEvent);
        Assert.IsTrue(store.Repoint(second).Succeeded);
        Assert.AreEqual(second, store.FilePath);
        store.Reloaded += _ =>
        {
            if (string.Equals(store.FilePath, third, StringComparison.OrdinalIgnoreCase)
                && store.Current.Map.ContainsKey("!third"))
            {
                recovered.Set();
            }
        };

        Volatile.Write(ref failThirdRead, 1);
        Assert.IsFalse(store.Repoint(third).Succeeded);
        staleFirstWatcherEvent!();

        Assert.IsTrue(recovered.Wait(TimeSpan.FromSeconds(5)), "The stale callback replaced the desired-transition retry.");
        Assert.AreEqual(third, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!third"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!second"));
    }

    [TestMethod]
    public void CandidateWatcherEventAfterHandoffReadReloadsPublishedState()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var reloaded = new ManualResetEventSlim(false);
        Action? raiseCandidateEvent = null;
        int useUpdatedContents = 0;
        byte[] ReadBytes(string file)
            => SnippetJson.Bytes(Volatile.Read(ref useUpdatedContents) == 0 ? "!old=before" : "!new=after");

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) => raiseCandidateEvent = handler,
            AfterWatcherHandoffRead = _ =>
            {
                Volatile.Write(ref useUpdatedContents, 1);
                raiseCandidateEvent!();
            },
        };

        using var store = NewStore(path, ReadBytes, hooks);
        Assert.IsTrue(store.Load().Succeeded);
        store.Reloaded += _ =>
        {
            if (store.Current.Map.ContainsKey("!new"))
            {
                reloaded.Set();
            }
        };

        Assert.IsTrue(store.StartWatching().Succeeded);

        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The candidate watcher event was dropped during publication.");
        Assert.IsTrue(store.Current.Map.ContainsKey("!new"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!old"));
    }

    [TestMethod]
    public void CandidateWatcherEventDoesNotLeakWhenHandoffFailsAndDisposesCandidate()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        Action? raiseCandidateEvent = null;
        int reloads = 0;
        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) => raiseCandidateEvent = handler,
            AfterWatcherHandoffRead = _ =>
            {
                raiseCandidateEvent!();
                throw new IOException("Injected failure after the watcher handoff read.");
            },
            BeforeWatcherReload = _ => Interlocked.Increment(ref reloads),
        };

        using var store = NewStore(path, hooks: hooks, retryDelay: TimeSpan.FromSeconds(30));
        Assert.IsTrue(store.Load().Succeeded);

        SnippetStore.TransitionResult result = store.StartWatching();
        raiseCandidateEvent!();
        Thread.Sleep(100);

        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.AreEqual("Injected failure after the watcher handoff read.", result.Error?.Message);
        Assert.IsFalse(store.IsWatching);
        Assert.AreEqual(0, Volatile.Read(ref reloads));
        Assert.IsTrue(store.Current.Map.ContainsKey("!old"));
    }

    [TestMethod]
    public void CandidateWatcherEventsDuringHandoffCoalesceIntoOneReload()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var reloaded = new ManualResetEventSlim(false);
        Action? raiseCandidateEvent = null;
        int useUpdatedContents = 0;
        int reloads = 0;
        byte[] ReadBytes(string file)
            => SnippetJson.Bytes(Volatile.Read(ref useUpdatedContents) == 0 ? "!old=before" : "!new=after");

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) => raiseCandidateEvent = handler,
            AfterWatcherHandoffRead = _ =>
            {
                Volatile.Write(ref useUpdatedContents, 1);
                raiseCandidateEvent!();
                raiseCandidateEvent();
                raiseCandidateEvent();
            },
            BeforeWatcherReload = _ => Interlocked.Increment(ref reloads),
        };

        using var store = NewStore(path, ReadBytes, hooks, debounceDelay: TimeSpan.FromMilliseconds(20));
        Assert.IsTrue(store.Load().Succeeded);
        store.Reloaded += _ => reloaded.Set();

        Assert.IsTrue(store.StartWatching().Succeeded);

        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The coalesced candidate watcher reload did not run.");
        Thread.Sleep(100);
        Assert.AreEqual(1, Volatile.Read(ref reloads));
        Assert.IsTrue(store.Current.Map.ContainsKey("!new"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!old"));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FailedRepointOrCaseSensitivityHandoffKeepsPriorStateAndWatcher(bool repoint)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "first.json", "Key=first", "key=second");
        string second = Write(scratch, "second.json", "!second=new");

        // The transition's first read stages it; its second, at the watcher handoff, fails.
        int failHandoff = 0;
        int transitionReads = 0;
        byte[] ReadBytes(string path)
        {
            if (Volatile.Read(ref failHandoff) != 0
                && string.Equals(path, repoint ? second : first, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Increment(ref transitionReads) == 2)
            {
                throw new IOException("Injected handoff read failure.");
            }

            return File.ReadAllBytes(path);
        }

        using var store = NewStore(first, ReadBytes, retryDelay: TimeSpan.FromSeconds(30));
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsTrue(store.IsWatching);
        Volatile.Write(ref failHandoff, 1);

        SnippetStore.TransitionResult result = repoint ? store.Repoint(second) : store.SetCaseSensitive(caseSensitive: false);

        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.IsInstanceOfType<IOException>(result.Error);
        Assert.IsTrue(result.HasCurrentState);
        Assert.AreEqual(first, store.FilePath);
        Assert.AreEqual(2, store.Current.Map.Count);
        Assert.IsFalse(store.Current.Map.TryGetValue("KEY", out _), "The old case-sensitive lookup must remain live.");
        Assert.IsFalse(store.Current.Map.ContainsKey("!second"));
        Assert.IsTrue(store.IsWatching, "The prior watcher must remain live until the full handoff commits.");
    }

    [TestMethod]
    public void WatcherRegistrationFailureIsExplicitAndKeepsLoadedState()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!ready=loaded");

        int failRegistration = 0;
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var hooks = new SnippetStore.TestHooks
        {
            BeforeWatcherActivation = _ =>
            {
                if (Interlocked.Exchange(ref failRegistration, 0) != 0)
                {
                    throw new IOException("Injected watcher registration failure.");
                }
            },
        };

        using var store = NewStore(path, hooks: hooks, retryDelay: TimeSpan.FromSeconds(30), log: logs.Enqueue);
        Assert.IsTrue(store.Load().Succeeded);
        Volatile.Write(ref failRegistration, 1);

        SnippetStore.TransitionResult result = store.StartWatching();

        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.IsInstanceOfType<IOException>(result.Error);
        Assert.AreEqual("Injected watcher registration failure.", result.Error?.Message);
        Assert.IsTrue(result.HasCurrentState);
        Assert.IsFalse(store.IsWatching);
        Assert.AreEqual(path, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!ready"));
        Assert.IsTrue(logs.Any(message => message.Contains("Injected watcher registration failure.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void WatcherErrorRetriesRegistrationAndReloadWithoutPublishingFailure()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var registrationFailed = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        Action<Exception>? raiseWatcherError = null;
        int useNewContents = 0;
        int failRegistration = 0;
        int reloadEvents = 0;

        byte[] ReadBytes(string file)
            => SnippetJson.Bytes(Volatile.Read(ref useNewContents) == 0 ? "!old=before" : "!new=after");

        var hooks = new SnippetStore.TestHooks
        {
            WatcherErrorHandlerRegistered = (_, handler) => raiseWatcherError = handler,
            BeforeWatcherActivation = _ =>
            {
                if (Interlocked.Exchange(ref failRegistration, 0) == 0)
                {
                    return;
                }

                registrationFailed.Set();
                if (!releaseFailure.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The watcher recovery gate timed out.");
                }

                throw new IOException("Injected recovery registration failure.");
            },
        };

        using var store = NewStore(path, ReadBytes, hooks, maxReloadAttempts: 4);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsNotNull(raiseWatcherError);
        store.Reloaded += _ =>
        {
            Interlocked.Increment(ref reloadEvents);
            reloaded.Set();
        };

        try
        {
            Volatile.Write(ref useNewContents, 1);
            Volatile.Write(ref failRegistration, 1);
            raiseWatcherError!(new IOException("Injected watcher error."));

            Assert.IsFalse(store.IsWatching);
            Assert.IsTrue(registrationFailed.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(store.Current.Map.ContainsKey("!old"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!new"));
            Assert.AreEqual(0, Volatile.Read(ref reloadEvents), "A failed re-registration must not raise Reloaded.");

            releaseFailure.Set();
            Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The watcher did not recover after the folder became watchable.");
            Assert.AreEqual(1, Volatile.Read(ref reloadEvents));
            Assert.IsTrue(store.IsWatching);
            Assert.IsTrue(store.Current.Map.ContainsKey("!new"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!old"));
        }
        finally
        {
            releaseFailure.Set();
        }
    }

    [TestMethod]
    public void FailedReloadKeepsTheLoadedSnippetsUntilRetrySucceeds()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var attempted = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        int obstructed = 0;
        int reloadEvents = 0;
        int reloadedCount = -1;

        byte[] ReadBytes(string file)
        {
            if (Volatile.Read(ref obstructed) != 0)
            {
                attempted.Set();
                if (!releaseFailure.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The deterministic reload gate timed out.");
                }

                throw new IOException("Injected reload read failure.");
            }

            return File.ReadAllBytes(file);
        }

        using var store = NewStore(path, ReadBytes, maxReloadAttempts: 4, log: logs.Enqueue);
        Assert.AreEqual(1, store.Load().Count);
        File.WriteAllText(path, SnippetJson.Of("!current=new"));
        Volatile.Write(ref obstructed, 1);
        store.Reloaded += count =>
        {
            reloadedCount = count;
            Interlocked.Increment(ref reloadEvents);
            reloaded.Set();
        };

        try
        {
            store.ScheduleReload();
            Assert.IsTrue(attempted.Wait(TimeSpan.FromSeconds(5)), "The first reload read did not start.");
            Assert.IsTrue(store.Current.Map.ContainsKey("!old"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!current"));
            Assert.AreEqual(0, Volatile.Read(ref reloadEvents), "A failed reload must not look successful.");

            Interlocked.Exchange(ref obstructed, 0);
            releaseFailure.Set();

            Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The bounded retry did not reload after the obstruction cleared.");
            Assert.AreEqual(1, reloadedCount);
            Assert.AreEqual(1, Volatile.Read(ref reloadEvents));
            Assert.IsTrue(store.Current.Map.ContainsKey("!current"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!old"));
            Assert.IsTrue(logs.Any(message => message.Contains("Retrying", StringComparison.Ordinal)));
        }
        finally
        {
            releaseFailure.Set();
        }
    }

    [TestMethod]
    public void WatcherReloadStopsAfterItsBoundedFailureCount()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var stopped = new ManualResetEventSlim(false);
        int failing = 0;
        int attempts = 0;
        int reloadEvents = 0;
        byte[] ReadBytes(string file)
        {
            if (Volatile.Read(ref failing) != 0)
            {
                Interlocked.Increment(ref attempts);
                throw new IOException("Injected persistent read failure.");
            }

            return File.ReadAllBytes(file);
        }

        using var store = NewStore(path, ReadBytes, log: SignalWhenLogged("Stopped retrying", stopped));
        Assert.AreEqual(1, store.Load().Count);
        Volatile.Write(ref failing, 1);
        store.Reloaded += _ => Interlocked.Increment(ref reloadEvents);

        store.ScheduleReload();

        Assert.IsTrue(stopped.Wait(TimeSpan.FromSeconds(5)), "The retry cycle did not reach its stop condition.");
        Assert.AreEqual(3, Volatile.Read(ref attempts));
        Thread.Sleep(100);
        Assert.AreEqual(3, Volatile.Read(ref attempts), "No unbounded retry may remain scheduled.");
        Assert.AreEqual(0, Volatile.Read(ref reloadEvents));
        Assert.IsTrue(store.Current.Map.ContainsKey("!old"));
    }

    [TestMethod]
    public void DisposeCancelsAPendingReloadRetry()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "snippets.json", "!old=before");

        using var attempted = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var retried = new ManualResetEventSlim(false);
        using var cancelled = new ManualResetEventSlim(false);
        int failing = 0;
        int attempts = 0;
        int reloadEvents = 0;
        byte[] ReadBytes(string file)
        {
            if (Volatile.Read(ref failing) != 0)
            {
                int attempt = Interlocked.Increment(ref attempts);
                if (attempt > 1)
                {
                    retried.Set();
                }

                attempted.Set();
                if (!releaseFailure.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The deterministic dispose gate timed out.");
                }

                throw new IOException("Injected reload read failure.");
            }

            return File.ReadAllBytes(file);
        }

        var store = NewStore(path, ReadBytes, maxReloadAttempts: 4, log: SignalWhenLogged("was disposed", cancelled));
        try
        {
            Assert.AreEqual(1, store.Load().Count);
            Volatile.Write(ref failing, 1);
            store.Reloaded += _ => Interlocked.Increment(ref reloadEvents);
            store.ScheduleReload();
            Assert.IsTrue(attempted.Wait(TimeSpan.FromSeconds(5)), "The failing reload did not start.");

            store.Dispose();
            releaseFailure.Set();

            Assert.IsTrue(cancelled.Wait(TimeSpan.FromSeconds(5)), "The in-flight failure did not observe disposal.");
            Assert.IsFalse(retried.IsSet, "Disposal must cancel every pending retry.");
            Assert.AreEqual(1, Volatile.Read(ref attempts));
            Assert.AreEqual(0, Volatile.Read(ref reloadEvents));
            Assert.IsTrue(store.Current.Map.ContainsKey("!old"));
        }
        finally
        {
            releaseFailure.Set();
            store.Dispose();
        }
    }

    [TestMethod]
    public void SettingsAndTheEngineAgreeOnTheSnippetFile()
    {
        Assert.AreEqual("PowerToysTextExpanderSnippets.json", SnippetStore.FileName);
        Assert.AreEqual(TextExpanderSnippetFileNames.Current, SnippetStore.FileName);
        Assert.AreEqual(SnippetStore.FileName, Path.GetFileName(SnippetStore.ResolveDefaultPath()));
    }

    [TestMethod]
    public void ParsingAndRepointingKeepTheLiveStoreObject()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Write(scratch, "a.json", "#one=first");
        string second = Write(scratch, "b.json", "#two=second", "#three=third");

        using var store = new SnippetStore(first);
        Assert.AreEqual(1, store.Load().Count);
        Assert.IsTrue(store.Current.Map.ContainsKey("#one"));
        SnippetStore.TransitionResult repoint = store.Repoint(second);
        Assert.AreEqual(SnippetStore.TransitionStatus.Succeeded, repoint.Status);
        Assert.AreEqual(2, repoint.Count);
        Assert.IsTrue(store.Current.Map.ContainsKey("#two"));
        Assert.IsFalse(store.Current.Map.ContainsKey("#one"));
        SnippetStore.TransitionResult unchanged = store.Repoint(second);
        Assert.AreEqual(SnippetStore.TransitionStatus.Unchanged, unchanged.Status);
    }

    [TestMethod]
    public void AFileTouchedButNotChangedKeepsItsSnippets()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "a.json", "#one=first");
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        SnippetStore.Snapshot loaded = store.Current;

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreSame(loaded, store.Current, "The same bytes aren't read into snippets again.");

        Assert.IsTrue(store.SetCaseSensitive(false).Succeeded);
        Assert.AreNotSame(loaded, store.Current, "Other matching rules get snippets of their own.");

        File.WriteAllText(path, SnippetJson.Of("#one=changed"));
        Assert.AreEqual(1, store.Load().Count);
        Assert.IsTrue(store.Current.Map.TryGetValue("#one", out string? replacement));
        Assert.AreEqual("changed", replacement);
    }

    [TestMethod]
    public void AWatcherReloadLetsTheSnippetsBeforeItGo()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "a.json", "#one=first");
        using var reloaded = new ManualResetEventSlim(false);
        using SnippetStore store = NewStore(path);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        WeakReference before = WeakCurrent(store);
        store.Reloaded += _ =>
        {
            if (store.Current.Map.ContainsKey("#two"))
            {
                reloaded.Set();
            }
        };

        File.WriteAllText(path, SnippetJson.Of("#two=second"));

        Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The watcher didn't reload the file.");
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    return !before.IsAlive;
                },
                TimeSpan.FromSeconds(5)),
            "The watcher keeps the snippets it started with.");
    }

    [TestMethod]
    public void ConfiguredPathFallsBackOnlyOnBlankAndInvalidValues()
    {
        string defaultPath = SnippetStore.ResolveDefaultPath();
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPathInfo(null, null).FilePath);
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPathInfo("   ", null).FilePath);
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string host = Path.Combine(scratch.Path, "host");
        string hostFile = Path.Combine(host, "PowerToysTextExpanderSnippets.json");
        Assert.AreEqual(hostFile, SnippetStore.ResolveConfiguredPathInfo(null, host).FilePath);
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPathInfo(null, "::\0 not a path").FilePath);

        // A value the shared selector rejects falls back to the host folder.
        Assert.AreEqual(hostFile, SnippetStore.ResolveConfiguredPathInfo(@"C:\" + new string('x', 40_000), host).FilePath);
        Assert.AreEqual(hostFile, SnippetStore.ResolveConfiguredPathInfo("relative.json", host).FilePath);

        // Any other path is the snippets file, even one that can't be there: it's reported, and nothing is created.
        string notAFolder = Path.Combine(Write(scratch, "file.json"), "inside.json");
        Assert.AreEqual(notAFolder, SnippetStore.ResolveConfiguredPathInfo(notAFolder, host).FilePath);
        string badName = Path.Combine(scratch.Path, "a<b", "snippets.json");
        Assert.AreEqual(badName, SnippetStore.ResolveConfiguredPathInfo(badName, host).FilePath);
        Assert.IsFalse(Directory.Exists(Path.Combine(scratch.Path, "a<b")));
    }

    [TestMethod]
    public void TheDefaultLocationGetsTheStarterInJson()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "default", SnippetStore.FileName);
        using var store = new SnippetStore(path, isDefault: true);

        Assert.AreEqual(6, store.Load().Count);
        Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreNotEqual(0xEF, bytes[0], "No byte order mark.");
        StringAssert.StartsWith(TextExpanderStarter.Content, "{" + Environment.NewLine + "  \"version\": 1," + Environment.NewLine + "  \"snippets\": [");

        // Deleted, it comes back the same.
        File.Delete(path);
        Assert.AreEqual(6, store.Load().Count);
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void AChosenFileThatIsMissingIsNeverCreatedAndHasNoSnippets()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();

        // In a folder that's there, and in one that isn't, such as a OneDrive folder before OneDrive starts at sign-in.
        string inFolder = Path.Combine(scratch.Path, "work.json");
        string folderGone = Path.Combine(scratch.Path, "OneDrive", "Text Expander", "work.json");
        foreach (string path in new[] { inFolder, folderGone })
        {
            SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(path, null);
            Assert.IsFalse(resolution.IsDefault);
            using SnippetStore store = NewStore(resolution.FilePath, isDefault: resolution.IsDefault, log: logs.Enqueue);

            SnippetStore.TransitionResult first = store.Load();
            SnippetStore.TransitionResult second = store.Load();

            Assert.IsTrue(first.Succeeded, path);
            Assert.AreEqual(0, first.Count);
            Assert.IsTrue(second.Succeeded);
            Assert.IsTrue(store.HasLoadedState, "No snippets is the state, not a failure that keeps old snippets.");
            Assert.AreEqual(0, store.Current.Map.Count);
            Assert.AreEqual(path, store.FilePath);
            Assert.IsFalse(File.Exists(path), "No starter is written there.");
        }

        Assert.AreEqual(0, Directory.GetFiles(scratch.Path).Length);
        Assert.IsFalse(Directory.Exists(Path.Combine(scratch.Path, "OneDrive")), "No folder is created either.");
        Assert.AreEqual(2, logs.Count(message => message.Contains("has no snippets until", StringComparison.Ordinal)), "Logged once for each file.");
    }

    [TestMethod]
    public void AChosenFileThatDisappearsLeavesNoSnippetsAndComesBackWithTheFile()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Write(scratch, "work.json", "!kept=Kept");
        using SnippetStore store = NewStore(path, maxReloadAttempts: 1);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.Current.Map.ContainsKey("!kept"));

        // Deleted on another PC, or moved: no snippets, rather than the old ones or a new starter.
        File.Delete(path);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.AreEqual(0, store.Current.Map.Count);
        Assert.IsFalse(File.Exists(path));

        File.WriteAllText(path, SnippetJson.Of("!back=Back"));
        Assert.AreEqual(1, store.Load().Count);
        Assert.IsTrue(store.Current.Map.ContainsKey("!back"));
    }

    [TestMethod]
    public void TheWatcherLoadsAChosenFileThatArrivesLater()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "work.json");
        using var arrived = new ManualResetEventSlim(false);
        using SnippetStore store = NewStore(path);
        store.Reloaded += count =>
        {
            if (count == 1)
            {
                arrived.Set();
            }
        };

        Assert.AreEqual(0, store.Load().Count);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsTrue(store.IsWatching, "The folder is there, so the file is watched for.");

        File.WriteAllText(path, SnippetJson.Of("!arrived=Arrived"));

        Assert.IsTrue(arrived.Wait(TimeSpan.FromSeconds(5)), "The file wasn't loaded when it arrived.");
        Assert.IsTrue(store.Current.Map.ContainsKey("!arrived"));
    }

    [TestMethod]
    public void RecoveryQuietlyLoadsAFileWhoseFolderArrivesLater()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string folder = Path.Combine(scratch.Path, "OneDrive", "Text Expander");
        string path = Path.Combine(folder, "work.json");
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var recovered = new ManualResetEventSlim(false);
        int reads = 0;
        byte[] ReadBytes(string file)
        {
            Interlocked.Increment(ref reads);
            return File.ReadAllBytes(file);
        }

        using SnippetStore store = NewStore(path, ReadBytes, maxReloadAttempts: 1, log: logs.Enqueue, recoveryInterval: TimeSpan.FromMilliseconds(50));
        store.Reloaded += count =>
        {
            if (count == 1)
            {
                recovered.Set();
            }
        };

        Assert.AreEqual(0, store.Load().Count);
        Assert.IsFalse(store.StartWatching().Succeeded, "There's no folder to watch yet.");
        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref reads) > 6, TimeSpan.FromSeconds(5)), "Recovery keeps trying.");
        int logged = logs.Count;
        Thread.Sleep(200);
        Assert.AreEqual(logged, logs.Count, "Each try is quiet.");
        Assert.IsFalse(Directory.Exists(folder), "Recovery creates nothing.");

        Directory.CreateDirectory(folder);
        File.WriteAllText(path, SnippetJson.Of("!synced=Synced"));

        Assert.IsTrue(recovered.Wait(TimeSpan.FromSeconds(5)), "The file wasn't loaded once its folder arrived.");
        Assert.IsTrue(store.Current.Map.ContainsKey("!synced"));
        Assert.IsTrue(store.IsWatching);

        // Loaded and watched, recovery stops reading.
        Thread.Sleep(200);
        int readsWhenLoaded = Volatile.Read(ref reads);
        Thread.Sleep(300);
        Assert.AreEqual(readsWhenLoaded, Volatile.Read(ref reads));
    }

    [TestMethod]
    public void RecoveryLoadsAFirstFileThatWasNotJsonOnceItIs()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "work.json");
        File.WriteAllText(path, "!old=text format");
        using var recovered = new ManualResetEventSlim(false);
        using SnippetStore store = NewStore(path, maxReloadAttempts: 1, recoveryInterval: TimeSpan.FromMilliseconds(50));
        store.Reloaded += _ => recovered.Set();

        SnippetStore.TransitionResult load = store.Load();
        Assert.IsFalse(load.Succeeded);
        Assert.IsInstanceOfType<SnippetFileFormatException>(load.Error);
        Assert.IsFalse(store.HasLoadedState);
        Assert.IsFalse(store.StartWatching().Succeeded);
        Assert.AreEqual("!old=text format", File.ReadAllText(path), "A file that isn't JSON is never written over.");

        File.WriteAllText(path, SnippetJson.Of("!fixed=Fixed"));

        Assert.IsTrue(recovered.Wait(TimeSpan.FromSeconds(5)), "The fixed file wasn't loaded.");
        Assert.IsTrue(store.Current.Map.ContainsKey("!fixed"));
        Assert.IsTrue(store.IsWatching);
    }

    [TestMethod]
    public void StarterCreationDoesNotOverwriteAConcurrentWinner()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();

        for (int iteration = 0; iteration < 16; iteration++)
        {
            string path = Path.Combine(scratch.Path, $"race-{iteration}.json");
            string externalText = SnippetJson.Of($"!external{iteration}=kept");
            byte[] externalBytes = new System.Text.UTF8Encoding(false).GetBytes(externalText);
            using var start = new ManualResetEventSlim(false);

            Task<bool> starter = Task.Run(() =>
            {
                start.Wait();
                return TextExpanderStarter.TryCreateFile(path);
            });
            Task<bool> external = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.Write(externalBytes);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            });

            start.Set();
            Task.WaitAll(starter, external);

            Assert.AreNotEqual(starter.Result, external.Result, "Exactly one create-if-absent writer wins.");
            if (starter.Result)
            {
                Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(path));
            }
            else
            {
                CollectionAssert.AreEqual(externalBytes, File.ReadAllBytes(path));
            }
        }

        Assert.AreEqual(0, Directory.GetFiles(scratch.Path, "*.tmp-*").Length);
    }

    [TestMethod]
    public void StarterStagingCleanupDeletesTheOwnedFileAfterAConcurrentWinner()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            beforeMove: () => File.WriteAllBytes(path, WinnerBytes),
            beforeStagingCleanup: null,
            beforeStagingCleanupDisposition: null);

        HandleBoundFileDeletionResult cleanup = AssertStagingCleanup(result, created: false, HandleBoundFileDeletionStatus.Deleted);
        StringAssert.StartsWith(cleanup.Path, path + ".tmp-");
        CollectionAssert.AreEqual(WinnerBytes, File.ReadAllBytes(path));
        Assert.AreEqual(0, Directory.GetFiles(scratch.Path, "*.tmp-*").Length);
    }

    [TestMethod]
    public void StarterStagingReplacementBeforeDispositionSurvivesAndIsReported()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=staging-replacement\r\n");
        string replacementPath = string.Empty;

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            beforeMove: () => File.WriteAllBytes(path, WinnerBytes),
            beforeStagingCleanup: null,
            beforeStagingCleanupDisposition: stagingPath =>
            {
                replacementPath = stagingPath;
                File.Move(stagingPath, stagingPath + ".owned");
                File.WriteAllBytes(stagingPath, replacementBytes);
            });

        HandleBoundFileDeletionResult cleanup = AssertStagingCleanup(result, created: false, HandleBoundFileDeletionStatus.PathStillPresent, replacementPath);
        StringAssert.Contains(cleanup.Error?.Message, cleanup.Path);
        CollectionAssert.AreEqual(WinnerBytes, File.ReadAllBytes(path));
        CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
        Assert.IsFalse(File.Exists(replacementPath + ".owned"), "Disposition must delete the verified original staging object after its rename.");
    }

    [TestMethod]
    public void StarterPublicationSurvivesAnUnownedStagingReplacementBeforeCleanup()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=staging-replacement\r\n");
        string replacementPath = string.Empty;

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            beforeMove: null,
            beforeStagingCleanup: stagingPath =>
            {
                replacementPath = stagingPath;
                File.WriteAllBytes(stagingPath, replacementBytes);
            },
            beforeStagingCleanupDisposition: null);

        AssertStagingCleanup(result, created: true, HandleBoundFileDeletionStatus.IdentityMismatch, replacementPath);
        Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(path));
        CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
    }

    [TestMethod]
    public void StarterStagingCleanupReportsHandleAcquisitionFailure()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        FileStream? blocker = null;
        string stagingPath = string.Empty;

        try
        {
            TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
                path,
                beforeMove: () =>
                {
                    File.WriteAllBytes(path, WinnerBytes);
                    stagingPath = Directory.GetFiles(scratch.Path, "*.tmp-*").Single();
                    blocker = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.None);
                },
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null);

            AssertStagingCleanup(result, created: false, HandleBoundFileDeletionStatus.HandleAcquisitionFailed, stagingPath);
            Assert.IsTrue(File.Exists(stagingPath));
            CollectionAssert.AreEqual(WinnerBytes, File.ReadAllBytes(path));
        }
        finally
        {
            blocker?.Dispose();
        }

        Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(stagingPath), "The staging file left behind still holds the starter.");
    }

    [TestMethod]
    public void StarterStagingCleanupReportsDispositionFailure()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        string stagingPath = string.Empty;

        try
        {
            TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
                path,
                beforeMove: () =>
                {
                    File.WriteAllBytes(path, WinnerBytes);
                    stagingPath = Directory.GetFiles(scratch.Path, "*.tmp-*").Single();
                    File.SetAttributes(stagingPath, File.GetAttributes(stagingPath) | FileAttributes.ReadOnly);
                },
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null);

            AssertStagingCleanup(result, created: false, HandleBoundFileDeletionStatus.DispositionFailed, stagingPath);
            Assert.IsTrue(File.Exists(stagingPath));
            CollectionAssert.AreEqual(WinnerBytes, File.ReadAllBytes(path));
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagingPath) && File.Exists(stagingPath))
            {
                File.SetAttributes(stagingPath, FileAttributes.Normal);
            }
        }
    }

    [TestMethod]
    public void StarterSnippetsUseBangTriggersAndNoRuntimeInput()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, SnippetStore.FileName);
        using var store = new SnippetStore(path, isDefault: true);
        Assert.AreEqual(6, store.Load().Count);

        Dictionary<string, string> starters = SnippetStore.Parse(File.ReadAllBytes(path));
        CollectionAssert.AreEquivalent(StarterTriggers, starters.Keys.ToArray());
        Assert.AreEqual("{{date}}", starters["!ddate"]);
        Assert.AreEqual("{{time}}", starters["!ttime"]);
        Assert.AreEqual("{{datetime}}", starters["!ddt"]);
        Assert.AreEqual("{{isodate}}", starters["!isodate"]);
        Assert.AreEqual("Best regards,\nYour name", starters["!mysig"]);
        Assert.AreEqual("you@example.com", starters["!myemail"]);

        foreach ((string trigger, string replacement) in starters)
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out _), trigger);

            // An accidental trigger must never paste the clipboard or open a prompt.
            Assert.IsFalse(replacement.Contains("{{clipboard}}", StringComparison.Ordinal), trigger);
            Assert.IsFalse(Variables.NeedsPrompt(replacement), trigger);

            // Without word mode a trigger that prefixes another would always fire first.
            Assert.IsFalse(starters.Keys.Any(other => other != trigger && other.StartsWith(trigger, StringComparison.Ordinal)), trigger);
        }

        // An existing library is never replaced by the starters.
        string mine = SnippetJson.Of("!mine=kept");
        File.WriteAllText(path, mine);
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(mine, File.ReadAllText(path));
    }

    /// <summary>A store with fast retries and no debounce, so failure and race tests run quickly.</summary>
    private static SnippetStore NewStore(
        string path,
        Func<string, byte[]>? readAllBytes = null,
        SnippetStore.TestHooks? hooks = null,
        bool isDefault = false,
        TimeSpan? retryDelay = null,
        int maxReloadAttempts = 3,
        Action<string>? log = null,
        TimeSpan? debounceDelay = null,
        TimeSpan? recoveryInterval = null)
        => new(
            path,
            isDefault,
            caseSensitive: true,
            readAllBytes ?? File.ReadAllBytes,
            debounceDelay ?? TimeSpan.Zero,
            retryDelay ?? TimeSpan.FromMilliseconds(10),
            maxReloadAttempts,
            log,
            hooks,
            recoveryInterval);

    /// <summary>Writes a snippets file with <paramref name="snippets"/>, each given as trigger=replacement.</summary>
    private static string Write(ScratchDirectory scratch, string name, params string[] snippets)
    {
        string path = Path.Combine(scratch.Path, name);
        File.WriteAllText(path, SnippetJson.Of(snippets));
        return path;
    }

    /// <summary>Not inlined, so no local of the test's own keeps the snippets alive.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference WeakCurrent(SnippetStore store) => new(store.Current);

    private static Action<string> SignalWhenLogged(string text, ManualResetEventSlim signal)
        => message =>
        {
            if (message.Contains(text, StringComparison.Ordinal))
            {
                signal.Set();
            }
        };

    private static HandleBoundFileDeletionResult AssertStagingCleanup(
        TextExpanderStarter.FileResult result,
        bool created,
        HandleBoundFileDeletionStatus status,
        string? stagingPath = null)
    {
        Assert.AreEqual(created, result.Created);
        Assert.IsTrue(result.StagingCleanup.HasValue);
        HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
        Assert.AreEqual(status, cleanup.Status);
        Assert.AreEqual(status == HandleBoundFileDeletionStatus.Deleted, cleanup.Succeeded);
        if (stagingPath is not null)
        {
            Assert.AreEqual(stagingPath, cleanup.Path);
        }

        return cleanup;
    }
}
