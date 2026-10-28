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
    private static readonly string[] ExampleTriggers = ["!hello", "!mdlink", "!longdate", "!time24", "!todo", "!myaddr", "!tabhead", "!meeting"];

    [TestMethod]
    public void ConfiguredPathUsesFolderFileOrHostDefault()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string folder = Path.Combine(scratch.Path, "library");
        Assert.AreEqual(Path.Combine(folder, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(folder, null));
        Assert.IsTrue(Directory.Exists(folder));

        string direct = Path.Combine(scratch.Path, "direct.txt");
        File.WriteAllText(direct, "#x=y");
        Assert.AreEqual(direct, SnippetStore.ResolveConfiguredPath(direct, null));

        string host = Path.Combine(scratch.Path, "host");
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath("relative", host));
    }

    [TestMethod]
    public void AFolderUsesTheNewNameThenTheLegacyName()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        foreach ((bool hasNew, bool hasLegacy, string expected) in new[]
        {
            (false, false, "PowerToysTextExpanderSnippets.txt"),
            (false, true, "snippets.txt"),
            (true, false, "PowerToysTextExpanderSnippets.txt"),
            (true, true, "PowerToysTextExpanderSnippets.txt"),
        })
        {
            string folder = Path.Combine(scratch.Path, $"new-{hasNew}-legacy-{hasLegacy}");
            Directory.CreateDirectory(folder);
            if (hasNew)
            {
                File.WriteAllText(Path.Combine(folder, "PowerToysTextExpanderSnippets.txt"), "!new=1\n");
            }

            if (hasLegacy)
            {
                File.WriteAllText(Path.Combine(folder, "snippets.txt"), "!old=1\n");
            }

            string want = Path.Combine(folder, expected);
            Assert.AreEqual(want, SnippetStore.ResolveInFolder(folder), folder);
            Assert.AreEqual(want, SnippetStore.ResolveConfiguredPath(folder, null), "Configured folder: " + folder);
            Assert.AreEqual(want, SnippetStore.ResolveConfiguredPath(null, folder), "Host folder: " + folder);
        }

        // A file path is used as it is, whatever its name.
        string direct = Path.Combine(scratch.Path, "new-False-legacy-True", "snippets.txt");
        Assert.AreEqual(direct, SnippetStore.ResolveConfiguredPath(direct, null));
    }

    [TestMethod]
    public void AFolderStoreSwitchesWhenThePreferredFileArrivesOrLeaves()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        File.WriteAllText(legacy, "!legacy=old\n");

        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(scratch.Path, null);
        Assert.IsTrue(resolution.ResolveFolderNames);
        using var store = new SnippetStore(resolution.FilePath, resolution.ResolveFolderNames);
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(legacy, store.FilePath);

        File.WriteAllText(current, "!current=new\n");
        Assert.AreEqual(1, store.ReloadPreferredPath().Count);
        Assert.AreEqual(current, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!current"));

        File.Delete(current);
        Assert.AreEqual(1, store.ReloadPreferredPath().Count);
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));

        SnippetPathResolution direct = SnippetStore.ResolveConfiguredPathInfo(legacy, null);
        Assert.IsFalse(direct.ResolveFolderNames, "An explicit file path stays on that file.");
    }

    [TestMethod]
    public void FolderAwareLoadRechecksLegacyAfterInitialResolution()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(scratch.Path, null);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=arrived\r\n");

        File.WriteAllBytes(legacy, legacyBytes);

        using var store = new SnippetStore(resolution.FilePath, resolution.ResolveFolderNames);
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
        CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
        Assert.IsFalse(File.Exists(current), "The starter must not hide a legacy database that arrived after resolution.");
    }

    [TestMethod]
    public void FolderClaimRechecksLegacyAtStarterPublicationBoundary()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=published-during-claim\r\n");

        TextExpanderStarter.FileResult result = TextExpanderStarter.ResolveOrCreateFileInFolder(
            scratch.Path,
            SnippetStore.FileName,
            SnippetStore.LegacyFileName,
            TimeSpan.FromSeconds(1),
            () => File.WriteAllBytes(legacy, legacyBytes));

        Assert.AreEqual(legacy, result.FilePath);
        Assert.IsFalse(result.Created);
        CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
        Assert.IsFalse(File.Exists(current), "No current-name sibling may be published beside the legacy winner.");
    }

    [TestMethod]
    public void FinalStarterMoveWindowYieldsToANonParticipatingLegacyWriter()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=published-after-final-check\r\n");

        TextExpanderStarter.FileResult result = TextExpanderStarter.ResolveOrCreateFileInFolder(
            scratch.Path,
            SnippetStore.FileName,
            SnippetStore.LegacyFileName,
            TimeSpan.FromSeconds(1),
            beforeStarterPublication: null,
            beforeStarterMove: () => File.WriteAllBytes(legacy, legacyBytes));

        Assert.AreEqual(legacy, result.FilePath);
        Assert.IsFalse(result.Created);
        CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
        Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterFile(current));
        Assert.AreEqual(3258L, new FileInfo(current).Length, "The authoritative starter bytes must stay unchanged.");

        Assert.AreEqual(legacy, TextExpanderStarter.ResolveOrCreateFileInFolder(scratch.Path, SnippetStore.FileName, SnippetStore.LegacyFileName).FilePath);
        Assert.AreEqual(legacy, SnippetStore.ResolveInFolder(scratch.Path));
        Assert.AreEqual(legacy, TextExpanderSnippetFileNames.ResolveInFolder(scratch.Path, File.Exists));

        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(scratch.Path, null);
        using var store = new SnippetStore(resolution.FilePath, resolution.ResolveFolderNames);
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
    }

    [TestMethod]
    public void InitialLoadFailureIsExplicitAndRetriesTheDesiredLibrary()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!ready=eventually\n");

        using var reloaded = new ManualResetEventSlim(false);
        int failures = 1;
        string[] ReadLines(string file)
        {
            if (Interlocked.Exchange(ref failures, 0) != 0)
            {
                throw new IOException("Injected initial read failure.");
            }

            return File.ReadAllLines(file);
        }

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3);
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
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        File.WriteAllText(first, "!first=old\n");
        File.WriteAllText(second, "!second=new\n");

        using var reloaded = new ManualResetEventSlim(false);
        int obstructed = 1;
        string[] ReadLines(string path)
        {
            if (string.Equals(path, second, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref obstructed, 0) != 0)
            {
                throw new IOException("Injected repoint failure.");
            }

            return File.ReadAllLines(path);
        }

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "Key=first\nkey=second\n");

        using var retryStarted = new ManualResetEventSlim(false);
        using var releaseRetry = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        int reads = 0;
        string[] ReadLines(string file)
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

            return File.ReadAllLines(file);
        }

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3);
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
        Assert.IsTrue(store.TryGetReplacement("KEY", out string replacement));
        Assert.AreEqual("second", replacement);
    }

    [TestMethod]
    public void QueuedOldWatcherReloadCannotOverwriteANewerRepoint()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        File.WriteAllText(first, "!first=old\n");
        File.WriteAllText(second, "!second=new\n");

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

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: File.ReadAllLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
        Assert.IsTrue(store.Load().Succeeded);

        store.ScheduleReload();
        Assert.IsTrue(captured.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(store.Repoint(second).Succeeded);
        Assert.AreEqual(second, store.FilePath);

        release.Set();
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, Volatile.Read(ref gateTimedOut), "The queued watcher gate timed out.");
        Assert.AreEqual(second, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!second"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!first"));
    }

    [TestMethod]
    public void QueuedOldWatcherReloadCannotUndoANewerCaseTransition()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "Key=first\nkey=second\n");

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

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: File.ReadAllLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
        Assert.IsTrue(store.Load().Succeeded);

        store.ScheduleReload();
        Assert.IsTrue(captured.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(store.SetCaseSensitive(caseSensitive: false).Succeeded);

        release.Set();
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, Volatile.Read(ref gateTimedOut), "The queued watcher gate timed out.");
        Assert.AreEqual(1, store.Current.Map.Count);
        Assert.IsTrue(store.TryGetReplacement("KEY", out string replacement));
        Assert.AreEqual("second", replacement);
    }

    [TestMethod]
    public void NewerWatcherEventDuringAnInflightReadAlwaysGetsASecondRead()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();

        for (int iteration = 0; iteration < 8; iteration++)
        {
            int run = iteration;
            string path = Path.Combine(scratch.Path, $"sequence-{run}.txt");
            File.WriteAllText(path, "!initial=zero\n");
            string[] contents = ["!initial=zero"];
            Action? raiseWatcherEvent = null;
            using var firstReadStarted = new ManualResetEventSlim(false);
            using var releaseFirstRead = new ManualResetEventSlim(false);
            using var secondReadStarted = new ManualResetEventSlim(false);
            using var finalReloaded = new ManualResetEventSlim(false);
            int trackReloads = 0;
            int reloadReads = 0;

            string[] ReadLines(string file)
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

                return captured;
            }

            var hooks = new SnippetStore.TestHooks
            {
                WatcherFileEventHandlerRegistered = (_, handler) =>
                    Interlocked.CompareExchange(ref raiseWatcherEvent, handler, null),
            };

            using var store = new SnippetStore(
                path,
                resolveFolderNames: false,
                caseSensitive: true,
                readAllLines: ReadLines,
                debounceDelay: TimeSpan.Zero,
                retryDelay: TimeSpan.FromMilliseconds(10),
                maxReloadAttempts: 3,
                testHooks: hooks);
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
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        File.WriteAllText(first, "!first=old\n");
        File.WriteAllText(second, "!second=new\n");

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

        string[] ReadLines(string path)
        {
            if (string.Equals(path, second, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref desiredAttempts);
                if (Volatile.Read(ref desiredReadable) == 0)
                {
                    throw new IOException("Injected unavailable desired library.");
                }

                return [.. Volatile.Read(ref desiredContents)];
            }

            return [.. Volatile.Read(ref fallbackContents)];
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseFallbackEvent, handler, null),
        };

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            log: message =>
            {
                if (message.Contains("Stopped retrying", StringComparison.Ordinal))
                {
                    int stopped = Interlocked.Increment(ref stopCount);
                    (stopped == 1 ? firstStop : secondStop).Set();
                }
            },
            testHooks: hooks);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "Key=first\nkey=second\n");

        string[] contents = ["Key=first", "key=second"];
        Action? raiseFallbackEvent = null;
        using var stopped = new ManualResetEventSlim(false);
        using var desiredReadStarted = new ManualResetEventSlim(false);
        using var releaseDesiredRead = new ManualResetEventSlim(false);
        using var recovered = new ManualResetEventSlim(false);
        int failReads = 0;
        int recoveryReads = -100;

        string[] ReadLines(string file)
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

            return captured;
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseFallbackEvent, handler, null),
        };

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            log: message =>
            {
                if (message.Contains("Stopped retrying", StringComparison.Ordinal))
                {
                    stopped.Set();
                }
            },
            testHooks: hooks);
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
            if (store.Current.Map.Count == 1 && store.TryGetReplacement("KEY", out string value) && value == "desired")
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
        Assert.IsTrue(store.TryGetReplacement("KEY", out string replacement));
        Assert.AreEqual("desired", replacement);
        Assert.IsTrue(Volatile.Read(ref recoveryReads) >= 3, "Recovery must include fallback, desired-stage, and watcher-handoff reads.");
    }

    [TestMethod]
    public void StalePublishedWatcherEventIsSuppressedAfterNewerTransitionCommits()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        File.WriteAllText(first, "!first=old\n");
        File.WriteAllText(second, "!second=new\n");

        Action? firstWatcherEvent = null;
        int firstReadsAfterCommit = 0;
        int trackFirstReads = 0;
        using var staleCompleted = new ManualResetEventSlim(false);
        string[] ReadLines(string path)
        {
            if (Volatile.Read(ref trackFirstReads) != 0
                && string.Equals(path, first, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref firstReadsAfterCommit);
            }

            return File.ReadAllLines(path);
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref firstWatcherEvent, handler, null),
            AfterWatcherReload = _ => staleCompleted.Set(),
        };

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!old=before\n");

        Action<Exception>? raiseWatcherError = null;
        Action? staleFileEvent = null;
        using var recovered = new ManualResetEventSlim(false);
        int useNewContents = 0;
        string[] ReadLines(string file)
            => Volatile.Read(ref useNewContents) == 0 ? ["!old=before"] : ["!new=after"];

        var hooks = new SnippetStore.TestHooks
        {
            WatcherErrorHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref raiseWatcherError, handler, null),
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref staleFileEvent, handler, null),
        };

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(150),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        string third = Path.Combine(scratch.Path, "third.txt");
        File.WriteAllText(first, "!first=one\n");
        File.WriteAllText(second, "!second=two\n");
        File.WriteAllText(third, "!third=three\n");

        Action? staleFirstWatcherEvent = null;
        using var recovered = new ManualResetEventSlim(false);
        int failThirdRead = 0;
        string[] ReadLines(string path)
        {
            if (string.Equals(path, third, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref failThirdRead, 0) != 0)
            {
                throw new IOException("Injected desired transition read failure.");
            }

            return File.ReadAllLines(path);
        }

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) =>
                Interlocked.CompareExchange(ref staleFirstWatcherEvent, handler, null),
        };

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(150),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
    public void WatcherHandoffReloadsLegacyArrivingAfterInitialStarterResolution()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        int published = 0;
        var hooks = new SnippetStore.TestHooks
        {
            BeforeWatcherActivation = _ =>
            {
                if (Interlocked.Exchange(ref published, 1) == 0)
                {
                    File.WriteAllText(legacy, "!legacy=arrived-in-handoff\n");
                }
            },
        };

        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(scratch.Path, null);
        using var store = new SnippetStore(
            resolution.FilePath,
            resolution.ResolveFolderNames,
            caseSensitive: true,
            readAllLines: File.ReadAllLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.AreEqual(current, store.FilePath);
        Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterFile(current));

        SnippetStore.TransitionResult handoff = store.StartWatching();

        Assert.IsTrue(handoff.Succeeded);
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
        Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterFile(current));
    }

    [TestMethod]
    public void CandidateWatcherEventAfterHandoffReadReloadsPublishedState()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!old=before\n");

        using var reloaded = new ManualResetEventSlim(false);
        Action? raiseCandidateEvent = null;
        int useUpdatedContents = 0;
        string[] ReadLines(string file)
            => Volatile.Read(ref useUpdatedContents) == 0 ? ["!old=before"] : ["!new=after"];

        var hooks = new SnippetStore.TestHooks
        {
            WatcherFileEventHandlerRegistered = (_, handler) => raiseCandidateEvent = handler,
            AfterWatcherHandoffRead = _ =>
            {
                Volatile.Write(ref useUpdatedContents, 1);
                raiseCandidateEvent!();
            },
        };

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!old=before\n");

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

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: File.ReadAllLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromSeconds(30),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!old=before\n");

        using var reloaded = new ManualResetEventSlim(false);
        Action? raiseCandidateEvent = null;
        int useUpdatedContents = 0;
        int reloads = 0;
        string[] ReadLines(string file)
            => Volatile.Read(ref useUpdatedContents) == 0 ? ["!old=before"] : ["!new=after"];

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

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.FromMilliseconds(20),
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            testHooks: hooks);
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
    public void FailedRepointHandoffKeepsPriorStateAndWatcher()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Path.Combine(scratch.Path, "first.txt");
        string second = Path.Combine(scratch.Path, "second.txt");
        File.WriteAllText(first, "!first=old\n");
        File.WriteAllText(second, "!second=new\n");

        int failSecondHandoff = 0;
        int secondReads = 0;
        string[] ReadLines(string path)
        {
            if (string.Equals(path, second, StringComparison.OrdinalIgnoreCase)
                && Volatile.Read(ref failSecondHandoff) != 0
                && Interlocked.Increment(ref secondReads) == 2)
            {
                throw new IOException("Injected repoint handoff read failure.");
            }

            return File.ReadAllLines(path);
        }

        using var store = new SnippetStore(
            first,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromSeconds(30),
            maxReloadAttempts: 3);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsTrue(store.IsWatching);
        Volatile.Write(ref failSecondHandoff, 1);

        SnippetStore.TransitionResult result = store.Repoint(second);

        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.IsInstanceOfType<IOException>(result.Error);
        Assert.IsTrue(result.HasCurrentState);
        Assert.AreEqual(first, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!first"));
        Assert.IsFalse(store.Current.Map.ContainsKey("!second"));
        Assert.IsTrue(store.IsWatching, "The prior watcher must remain live until the full handoff commits.");
    }

    [TestMethod]
    public void FailedCaseSensitivityHandoffKeepsPriorStateAndWatcher()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "Key=first\nkey=second\n");

        int failSecondHandoff = 0;
        int transitionReads = 0;
        string[] ReadLines(string file)
        {
            if (Volatile.Read(ref failSecondHandoff) != 0
                && Interlocked.Increment(ref transitionReads) == 2)
            {
                throw new IOException("Injected case-sensitivity handoff read failure.");
            }

            return File.ReadAllLines(file);
        }

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromSeconds(30),
            maxReloadAttempts: 3);
        Assert.IsTrue(store.Load().Succeeded);
        Assert.IsTrue(store.StartWatching().Succeeded);
        Assert.IsTrue(store.IsWatching);
        Volatile.Write(ref failSecondHandoff, 1);

        SnippetStore.TransitionResult result = store.SetCaseSensitive(caseSensitive: false);

        Assert.AreEqual(SnippetStore.TransitionStatus.Failed, result.Status);
        Assert.IsInstanceOfType<IOException>(result.Error);
        Assert.AreEqual(2, store.Current.Map.Count);
        Assert.IsFalse(store.TryGetReplacement("KEY", out _), "The old case-sensitive lookup must remain live.");
        Assert.IsTrue(store.IsWatching, "The prior watcher must remain live until the full handoff commits.");
    }

    [TestMethod]
    public void WatcherRegistrationFailureIsExplicitAndKeepsLoadedState()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!ready=loaded\n");

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

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: File.ReadAllLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromSeconds(30),
            maxReloadAttempts: 3,
            log: logs.Enqueue,
            testHooks: hooks);
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(path, "!old=before\n");

        using var registrationFailed = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        Action<Exception>? raiseWatcherError = null;
        int useNewContents = 0;
        int failRegistration = 0;
        int reloadEvents = 0;

        string[] ReadLines(string file)
            => Volatile.Read(ref useNewContents) == 0 ? ["!old=before"] : ["!new=after"];

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

        using var store = new SnippetStore(
            path,
            resolveFolderNames: false,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 4,
            testHooks: hooks);
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
    public void FailedPreferredReloadKeepsPathAndContentsPairedUntilRetrySucceeds()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        File.WriteAllText(legacy, "!legacy=old\n");

        using var attempted = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var reloaded = new ManualResetEventSlim(false);
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        int obstructed = 1;
        int reloadEvents = 0;
        int reloadedCount = -1;

        string[] ReadLines(string path)
        {
            if (string.Equals(path, current, StringComparison.OrdinalIgnoreCase)
                && Volatile.Read(ref obstructed) != 0)
            {
                attempted.Set();
                if (!releaseFailure.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The deterministic reload gate timed out.");
                }

                throw new IOException("Injected preferred-file read failure.");
            }

            return File.ReadAllLines(path);
        }

        using var store = new SnippetStore(
            legacy,
            resolveFolderNames: true,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 4,
            log: logs.Enqueue);
        Assert.AreEqual(1, store.Load().Count);
        File.WriteAllText(current, "!current=new\n");
        store.Reloaded += count =>
        {
            reloadedCount = count;
            Interlocked.Increment(ref reloadEvents);
            reloaded.Set();
        };

        try
        {
            store.ScheduleReload();
            Assert.IsTrue(attempted.Wait(TimeSpan.FromSeconds(5)), "The first preferred-file read did not start.");
            Assert.AreEqual(legacy, store.FilePath);
            Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!current"));
            Assert.AreEqual(0, Volatile.Read(ref reloadEvents), "A failed reload must not look successful.");

            Interlocked.Exchange(ref obstructed, 0);
            releaseFailure.Set();

            Assert.IsTrue(reloaded.Wait(TimeSpan.FromSeconds(5)), "The bounded retry did not reload after the obstruction cleared.");
            Assert.AreEqual(1, reloadedCount);
            Assert.AreEqual(1, Volatile.Read(ref reloadEvents));
            Assert.AreEqual(current, store.FilePath);
            Assert.IsTrue(store.Current.Map.ContainsKey("!current"));
            Assert.IsFalse(store.Current.Map.ContainsKey("!legacy"));
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
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        File.WriteAllText(legacy, "!legacy=old\n");

        using var stopped = new ManualResetEventSlim(false);
        int attempts = 0;
        int reloadEvents = 0;
        string[] ReadLines(string path)
        {
            if (string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref attempts);
                throw new IOException("Injected persistent read failure.");
            }

            return File.ReadAllLines(path);
        }

        using var store = new SnippetStore(
            legacy,
            resolveFolderNames: true,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 3,
            log: message =>
            {
                if (message.Contains("Stopped retrying", StringComparison.Ordinal))
                {
                    stopped.Set();
                }
            });
        Assert.AreEqual(1, store.Load().Count);
        File.WriteAllText(current, "!current=new\n");
        store.Reloaded += _ => Interlocked.Increment(ref reloadEvents);

        store.ScheduleReload();

        Assert.IsTrue(stopped.Wait(TimeSpan.FromSeconds(5)), "The retry cycle did not reach its stop condition.");
        Assert.AreEqual(3, Volatile.Read(ref attempts));
        Thread.Sleep(100);
        Assert.AreEqual(3, Volatile.Read(ref attempts), "No unbounded retry may remain scheduled.");
        Assert.AreEqual(0, Volatile.Read(ref reloadEvents));
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
    }

    [TestMethod]
    public void DisposeCancelsAPendingReloadRetry()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        File.WriteAllText(legacy, "!legacy=old\n");

        using var attempted = new ManualResetEventSlim(false);
        using var releaseFailure = new ManualResetEventSlim(false);
        using var retried = new ManualResetEventSlim(false);
        using var cancelled = new ManualResetEventSlim(false);
        int attempts = 0;
        int reloadEvents = 0;
        string[] ReadLines(string path)
        {
            if (string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
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

                throw new IOException("Injected preferred-file read failure.");
            }

            return File.ReadAllLines(path);
        }

        var store = new SnippetStore(
            legacy,
            resolveFolderNames: true,
            caseSensitive: true,
            readAllLines: ReadLines,
            debounceDelay: TimeSpan.Zero,
            retryDelay: TimeSpan.FromMilliseconds(10),
            maxReloadAttempts: 4,
            log: message =>
            {
                if (message.Contains("was disposed", StringComparison.Ordinal))
                {
                    cancelled.Set();
                }
            });
        try
        {
            Assert.AreEqual(1, store.Load().Count);
            File.WriteAllText(current, "!current=new\n");
            store.Reloaded += _ => Interlocked.Increment(ref reloadEvents);
            store.ScheduleReload();
            Assert.IsTrue(attempted.Wait(TimeSpan.FromSeconds(5)), "The failing reload did not start.");

            store.Dispose();
            releaseFailure.Set();

            Assert.IsTrue(cancelled.Wait(TimeSpan.FromSeconds(5)), "The in-flight failure did not observe disposal.");
            Assert.IsFalse(retried.IsSet, "Disposal must cancel every pending retry.");
            Assert.AreEqual(1, Volatile.Read(ref attempts));
            Assert.AreEqual(0, Volatile.Read(ref reloadEvents));
            Assert.AreEqual(legacy, store.FilePath);
            Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));
        }
        finally
        {
            releaseFailure.Set();
            store.Dispose();
        }
    }

    [TestMethod]
    public void ALegacyFileIsReadInPlaceAndNeverRenamed()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(legacy, "!mine=kept\n");

        string path = SnippetStore.ResolveConfiguredPath(scratch.Path, null);
        Assert.AreEqual(legacy, path);
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual("!mine=kept\n", File.ReadAllText(legacy));
        Assert.IsFalse(File.Exists(Path.Combine(scratch.Path, "PowerToysTextExpanderSnippets.txt")), "No starter file next to a legacy one.");

        // An empty folder gets the starter file under the new name.
        string empty = Path.Combine(scratch.Path, "empty");
        using var seeded = new SnippetStore(SnippetStore.ResolveConfiguredPath(empty, null));
        Assert.AreEqual(6, seeded.Load().Count);
        Assert.IsTrue(File.Exists(Path.Combine(empty, "PowerToysTextExpanderSnippets.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(empty, "snippets.txt")));
    }

    [TestMethod]
    public void SettingsAndTheEngineAgreeOnTheSnippetFile()
    {
        Assert.AreEqual("PowerToysTextExpanderSnippets.txt", SnippetStore.FileName);
        Assert.AreEqual("snippets.txt", SnippetStore.LegacyFileName);
        Assert.AreEqual(TextExpanderSnippetFileNames.Current, SnippetStore.FileName);
        Assert.AreEqual(TextExpanderSnippetFileNames.Legacy, SnippetStore.LegacyFileName);

        using ScratchDirectory scratch = ScratchDirectory.Create();
        foreach (int files in new[] { 0, 1, 2, 3 })
        {
            string folder = Path.Combine(scratch.Path, files.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            if ((files & 1) != 0)
            {
                File.WriteAllText(Path.Combine(folder, SnippetStore.FileName), "!a=1\n");
            }

            if ((files & 2) != 0)
            {
                File.WriteAllText(Path.Combine(folder, SnippetStore.LegacyFileName), "!b=1\n");
            }

            Assert.AreEqual(SnippetStore.ResolveInFolder(folder), TextExpanderSnippetFileNames.ResolveInFolder(folder, File.Exists), folder);
            Assert.AreEqual(files == 0 ? null : SnippetStore.ResolveInFolder(folder), TextExpanderSnippetFileNames.FindInFolder(folder, File.Exists), folder);
        }
    }

    [TestMethod]
    public void ParsingAndRepointingKeepTheLiveStoreObject()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Path.Combine(scratch.Path, "a");
        string second = Path.Combine(scratch.Path, "b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "snippets.txt"), "#one=first\n");
        File.WriteAllText(Path.Combine(second, "snippets.txt"), "#two=second\n#three=third\n");

        using var store = new SnippetStore(Path.Combine(first, "snippets.txt"));
        Assert.AreEqual(1, store.Load().Count);
        Assert.IsTrue(store.Current.Map.ContainsKey("#one"));
        SnippetStore.TransitionResult repoint = store.Repoint(Path.Combine(second, "snippets.txt"));
        Assert.IsTrue(repoint.Succeeded);
        Assert.IsTrue(repoint.Changed);
        Assert.AreEqual(2, repoint.Count);
        Assert.IsTrue(store.Current.Map.ContainsKey("#two"));
        Assert.IsFalse(store.Current.Map.ContainsKey("#one"));
        SnippetStore.TransitionResult unchanged = store.Repoint(Path.Combine(second, "snippets.txt"));
        Assert.IsTrue(unchanged.Succeeded);
        Assert.IsFalse(unchanged.Changed);
    }

    [TestMethod]
    public void ConfiguredPathFallsBackOnBlankAndInvalidValues()
    {
        string defaultPath = SnippetStore.ResolveDefaultPath();
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath(null, null));
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath("   ", null));
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string host = Path.Combine(scratch.Path, "host");
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath(null, "::\0 not a path"));
    }

    [TestMethod]
    public void ParserHandlesInlineBlocksCommentsAndDuplicateLastWriterWins()
    {
        Dictionary<string, string> parsed = SnippetStore.Parse([
            "// comment",
            "a=one",
            "block<<<",
            "line one",
            "line two",
            ">>>",
            "a=two"]);
        Assert.AreEqual("two", parsed["a"]);
        Assert.AreEqual("line one\nline two", parsed["block"]);
        Assert.AreEqual(2, parsed.Count);
    }

    [TestMethod]
    public void LoadCreatesExactSharedStarterWhenMissing()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        Assert.IsTrue(store.Load().Count >= 1);
        Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(path));
        Assert.AreEqual(3258L, new FileInfo(path).Length);
        Assert.AreEqual(
            "6537B122AD12BF898194818F0686EDB9ED8B99A13031075263006CCB1C7E2CA7",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
    }

    [TestMethod]
    public void StarterCreationDoesNotOverwriteAConcurrentWinner()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();

        for (int iteration = 0; iteration < 16; iteration++)
        {
            string path = Path.Combine(scratch.Path, $"race-{iteration}.txt");
            string externalText = $"!external{iteration}=kept\r\n";
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        byte[] winnerBytes = System.Text.Encoding.UTF8.GetBytes("!external=winner\r\n");

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            canPublish: null,
            beforeMove: () => File.WriteAllBytes(path, winnerBytes),
            beforeStagingCleanup: null,
            beforeStagingCleanupDisposition: null);

        Assert.IsFalse(result.Created);
        Assert.IsTrue(result.StagingCleanup.HasValue);
        HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
        Assert.IsTrue(cleanup.Succeeded);
        Assert.AreEqual(HandleBoundFileDeletionStatus.Deleted, cleanup.Status);
        StringAssert.StartsWith(cleanup.Path, path + ".tmp-");
        Assert.IsTrue(result.StagingCleanupSucceeded);
        Assert.IsNull(result.IncompleteStagingPath);
        CollectionAssert.AreEqual(winnerBytes, File.ReadAllBytes(path));
        Assert.AreEqual(0, Directory.GetFiles(scratch.Path, "*.tmp-*").Length);
    }

    [TestMethod]
    public void StarterStagingReplacementBeforeDispositionSurvivesAndIsReported()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        byte[] winnerBytes = System.Text.Encoding.UTF8.GetBytes("!external=winner\r\n");
        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=staging-replacement\r\n");
        string replacementPath = string.Empty;
        string ownedObjectPath = string.Empty;

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            canPublish: null,
            beforeMove: () => File.WriteAllBytes(path, winnerBytes),
            beforeStagingCleanup: null,
            beforeStagingCleanupDisposition: stagingPath =>
            {
                replacementPath = stagingPath;
                ownedObjectPath = stagingPath + ".owned";
                File.Move(stagingPath, ownedObjectPath);
                File.WriteAllBytes(stagingPath, replacementBytes);
            });

        Assert.IsFalse(result.Created);
        Assert.IsTrue(result.StagingCleanup.HasValue);
        HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
        Assert.IsFalse(cleanup.Succeeded);
        Assert.IsTrue(cleanup.OwnedObjectDeleted);
        Assert.AreEqual(HandleBoundFileDeletionStatus.PathStillPresent, cleanup.Status);
        Assert.AreEqual(replacementPath, cleanup.Path);
        Assert.AreEqual(cleanup.Path, result.IncompleteStagingPath);
        Assert.IsFalse(result.StagingCleanupSucceeded);
        StringAssert.Contains(cleanup.Error?.Message, cleanup.Path);
        CollectionAssert.AreEqual(winnerBytes, File.ReadAllBytes(path));
        CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
        Assert.IsFalse(File.Exists(ownedObjectPath), "Disposition must delete the verified original staging object after its rename.");
    }

    [TestMethod]
    public void StarterPublicationSurvivesAnUnownedStagingReplacementBeforeCleanup()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=staging-replacement\r\n");
        string replacementPath = string.Empty;

        TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
            path,
            canPublish: null,
            beforeMove: null,
            beforeStagingCleanup: stagingPath =>
            {
                replacementPath = stagingPath;
                File.WriteAllBytes(stagingPath, replacementBytes);
            },
            beforeStagingCleanupDisposition: null);

        Assert.IsTrue(result.Created);
        Assert.IsTrue(result.StagingCleanup.HasValue);
        HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
        Assert.IsFalse(cleanup.Succeeded);
        Assert.AreEqual(HandleBoundFileDeletionStatus.IdentityMismatch, cleanup.Status);
        Assert.AreEqual(replacementPath, cleanup.Path);
        Assert.AreEqual(cleanup.Path, result.IncompleteStagingPath);
        Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(path));
        Assert.AreEqual(3258L, new FileInfo(path).Length);
        CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
    }

    [TestMethod]
    public void StarterStagingCleanupReportsHandleAcquisitionFailure()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        byte[] winnerBytes = System.Text.Encoding.UTF8.GetBytes("!external=winner\r\n");
        FileStream? blocker = null;
        string stagingPath = string.Empty;

        try
        {
            TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
                path,
                canPublish: null,
                beforeMove: () =>
                {
                    File.WriteAllBytes(path, winnerBytes);
                    stagingPath = Directory.GetFiles(scratch.Path, "*.tmp-*").Single();
                    blocker = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.None);
                },
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null);

            Assert.IsFalse(result.Created);
            Assert.IsTrue(result.StagingCleanup.HasValue);
            HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
            Assert.IsFalse(cleanup.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.HandleAcquisitionFailed, cleanup.Status);
            Assert.AreEqual(stagingPath, cleanup.Path);
            Assert.AreEqual(cleanup.Path, result.IncompleteStagingPath);
            Assert.IsTrue(File.Exists(stagingPath));
            CollectionAssert.AreEqual(winnerBytes, File.ReadAllBytes(path));
        }
        finally
        {
            blocker?.Dispose();
        }

        Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterBytes(File.ReadAllBytes(stagingPath)));
    }

    [TestMethod]
    public void StarterStagingCleanupReportsDispositionFailure()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        byte[] winnerBytes = System.Text.Encoding.UTF8.GetBytes("!external=winner\r\n");
        string stagingPath = string.Empty;

        try
        {
            TextExpanderStarter.FileResult result = TextExpanderStarter.TryCreateFile(
                path,
                canPublish: null,
                beforeMove: () =>
                {
                    File.WriteAllBytes(path, winnerBytes);
                    stagingPath = Directory.GetFiles(scratch.Path, "*.tmp-*").Single();
                    File.SetAttributes(stagingPath, File.GetAttributes(stagingPath) | FileAttributes.ReadOnly);
                },
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null);

            Assert.IsFalse(result.Created);
            Assert.IsTrue(result.StagingCleanup.HasValue);
            HandleBoundFileDeletionResult cleanup = result.StagingCleanup.Value;
            Assert.IsFalse(cleanup.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.DispositionFailed, cleanup.Status);
            Assert.AreEqual(stagingPath, cleanup.Path);
            Assert.AreEqual(cleanup.Path, result.IncompleteStagingPath);
            Assert.IsTrue(File.Exists(stagingPath));
            CollectionAssert.AreEqual(winnerBytes, File.ReadAllBytes(path));
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
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        Assert.AreEqual(6, store.Load().Count);

        Dictionary<string, string> starters = SnippetStore.Parse(File.ReadAllLines(path));
        CollectionAssert.AreEquivalent(StarterTriggers, starters.Keys.ToArray());
        Assert.AreEqual("{{date}}", starters["!ddate"]);
        Assert.AreEqual("{{time}}", starters["!ttime"]);
        Assert.AreEqual("{{datetime}}", starters["!ddt"]);
        Assert.AreEqual("{{isodate}}", starters["!isodate"]);
        Assert.AreEqual(@"Best regards,\nYour Name", starters["!mysig"]);
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

        // The header documents every variable and the cursor marker.
        string text = File.ReadAllText(path);
        foreach (string token in new[] { "{{date}}", "{{time}}", "{{datetime}}", "{{isodate}}", "{{date:FORMAT}}", "{{date:dddd, MMMM d}}", "{{clipboard}}", "{{prompt:Question}}", "{{prompt?:Question}}", "$|$", "\\n", "\\t", "<<<", ">>>" })
        {
            StringAssert.Contains(text, token);
        }

        // An existing library is never replaced by the starters.
        File.WriteAllText(path, "!mine=kept\n");
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual("!mine=kept\n", File.ReadAllText(path));
    }

    [TestMethod]
    public void StarterFileOpensWithTheCredit()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        string[] lines = File.ReadAllLines(path);
        StringAssert.StartsWith(lines[0], "// ── PowerToys Text Expander ──");
        Assert.AreEqual("// Built by Gary Hsu · https://github.com/howdygary", lines[1]);
        Assert.AreEqual("// Project: https://github.com/howdygary/text-expander", lines[2]);
        Assert.AreEqual("// Website: https://howdygary.github.io/text-expander/", lines[3]);
    }

    [TestMethod]
    public void StarterGuideLinesUpAndItsDateTipWorks()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        // QA-21: every description in the variables guide starts in the same column.
        string[] lines = File.ReadAllLines(path);
        int start = Array.FindIndex(lines, l => l.Contains("── Variables", StringComparison.Ordinal));
        int end = Array.FindIndex(lines, l => l.Contains("── Tips", StringComparison.Ordinal));
        string[] guide = lines[(start + 1)..end].Where(line => line != "//").ToArray();
        int column = guide[0].IndexOf("today's date", StringComparison.Ordinal);
        Assert.IsTrue(guide.Length >= 10 && column > 0);
        foreach (string line in guide)
        {
            Assert.IsTrue(line.Length > column && line[column - 1] == ' ' && line[column] != ' ', line);
        }

        // The variables with nothing to show ahead of time each get an example.
        StringAssert.Contains(string.Join('\n', guide), "{{prompt:First name}}");
        Assert.IsTrue(guide.Any(line => line.StartsWith("// {{clipboard}}", StringComparison.Ordinal) && line.Contains("such as", StringComparison.Ordinal)));
        Assert.IsTrue(guide.Any(line => line.StartsWith("// $|$", StringComparison.Ordinal) && line.Contains("Dear $|$", StringComparison.Ordinal)));

        // The tip about words in a date format gives what it says.
        string tip = guide.Single(line => line.Contains("{{date:'", StringComparison.Ordinal));
        int open = tip.IndexOf("{{date:", StringComparison.Ordinal) + "{{date:".Length;
        int close = tip.IndexOf("}}", open, StringComparison.Ordinal);
        string said = tip[(tip.IndexOf(" gives ", StringComparison.Ordinal) + " gives ".Length)..];
        Assert.AreEqual(said, Variables.FormatDate(new DateTime(2026, 6, 1), tip[open..close]));
        Assert.AreNotEqual(said, Variables.FormatDate(new DateTime(2026, 6, 1), tip[open..close].Replace("'", string.Empty, StringComparison.Ordinal)), "Without the quotes the word is read as codes.");
    }

    [TestMethod]
    public void StarterExamplesWorkOnceUncommented()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        // Do exactly what the heading says, on every line of the examples.
        string[] lines = File.ReadAllLines(path);
        int heading = Array.FindIndex(lines, l => l.Contains("── Examples:", StringComparison.Ordinal));
        StringAssert.Contains(lines[heading], "remove the // and the space after it", "QA-6: the instruction names exactly what the test removes.");
        int first = heading + 1;
        int last = Array.FindIndex(lines, l => l.Contains("── Your snippets", StringComparison.Ordinal));
        Assert.IsTrue(first > 0 && last > first, "The examples sit between their heading and Your snippets.");
        Assert.IsTrue(lines[first..last].Where(line => line != "//").All(line => line.StartsWith("// ", StringComparison.Ordinal)), "Every example line has the // and the space to remove.");
        string[] uncommented = lines
            .Select((line, i) => i >= first && i < last && line != "//" ? line["// ".Length..] : line)
            .ToArray();

        Dictionary<string, string> all = SnippetStore.Parse(uncommented);
        CollectionAssert.AreEquivalent(StarterTriggers.Concat(ExampleTriggers).ToArray(), all.Keys.ToArray());
        Assert.AreEqual("Hi {{prompt:First name}}, thanks for reaching out!", all["!hello"]);
        Assert.AreEqual("[{{prompt:Link text}}]({{clipboard}})", all["!mdlink"]);
        Assert.AreEqual("{{date:dddd, MMMM d, yyyy}}", all["!longdate"]);
        Assert.AreEqual("{{date:HH:mm}}", all["!time24"]);
        Assert.AreEqual("// TODO: $|$ ({{isodate}})", all["!todo"]);
        Assert.AreEqual(@"123 Example Street\nSpringfield, ZZ 12345", all["!myaddr"]);
        Assert.AreEqual(@"Name\tEmail\tTeam", all["!tabhead"]);
        Assert.AreEqual("Meeting notes · {{date:dddd, MMMM d, yyyy}}\nAttendees: $|$\nNext steps:", all["!meeting"]);

        foreach (string trigger in all.Keys)
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out _), trigger);

            // Without word mode a trigger that starts another would always fire first.
            Assert.IsFalse(all.Keys.Any(other => other != trigger && other.StartsWith(trigger, StringComparison.Ordinal)), trigger);
        }

        // Left commented, the examples stay inert: only the six starters load.
        Assert.AreEqual(6, SnippetStore.Parse(lines).Count);
    }
}
