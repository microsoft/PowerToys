// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Telemetry;

namespace MouseJump.Common.UnitTests.Telemetry;

[TestClass]
public sealed class TelemetryContextTests
{
    /// <summary>
    /// Upper bound for anything that blocks (flush, stop, worker threads) - a bug in the
    /// adapter's consumer loop shows up as a hang, so every blocking call goes through
    /// <see cref="WithinTimeout"/> to turn that into a test failure instead.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void AScopeWritesNoRecordOfItsOwnButItsPropertiesReachRecordsInsideIt()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        using (telemetry.BeginScope(new { activation = 42 }, "outerScope"))
        {
            telemetry.WriteEvent(new { step = "inside" }, "innerEvent");
        }

        telemetry.Stop();

        var record = writer.Records.Single();
        Assert.AreEqual("innerEvent", record.Operation);
        Assert.AreEqual("event", record.Kind);
        Assert.AreEqual(42, record.Properties["activation"]);
        Assert.AreEqual("inside", record.Properties["step"]);
    }

    [TestMethod]
    public void AnInnerScopesPropertyWinsOverAnOuterScopesOnKeyCollision()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        using (telemetry.BeginScope(new { outerOnly = 1, shared = "outer" }, "outerScope"))
        using (telemetry.BeginScope(new { shared = "inner" }, "innerScope"))
        {
            telemetry.WriteEvent(new { }, "innerEvent");
        }

        telemetry.Stop();

        var record = writer.Records.Single();
        Assert.AreEqual(1, record.Properties["outerOnly"]);
        Assert.AreEqual("inner", record.Properties["shared"]);
    }

    [TestMethod]
    public void RecordsReachTheWriterInTheOrderTheyWereRecorded()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        for (var i = 0; i < 100; i++)
        {
            telemetry.WriteEvent(new { index = i }, "orderedEvent");
        }

        TelemetryContextTests.WithinTimeout(telemetry.Flush);

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 100).ToList(),
            writer.Records.Select(record => (int)record.Properties["index"]!).ToList());

        TelemetryContextTests.WithinTimeout(telemetry.Stop);
    }

    [TestMethod]
    public void AFlushWhilePausedWaitsForResumeAndNothingIsWrittenMeanwhile()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        telemetry.PauseWriting();
        telemetry.WriteEvent(new { }, "pausedEvent");
        var flush = Task.Run(telemetry.Flush);

        // a negative check, so it can only ever wait "a while" - long enough that a flush
        // which ignored the pause would almost certainly have finished by now
        Assert.IsFalse(flush.Wait(TimeSpan.FromMilliseconds(250)), "flush should block while writing is paused");
        Assert.AreEqual(0, writer.Records.Count, "nothing should reach the writer while paused");

        telemetry.ResumeWriting();
        Assert.IsTrue(flush.Wait(TelemetryContextTests.Timeout), "flush should complete once writing resumes");
        Assert.AreEqual("pausedEvent", writer.Records.Single().Operation);

        TelemetryContextTests.WithinTimeout(telemetry.Stop);
    }

    [TestMethod]
    public void ConcurrentWritesAndFlushesLoseNothingAndKeepEachThreadsOwnOrder()
    {
        const int threadCount = 8;
        const int eventsPerThread = 200;

        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        var workers = Enumerable.Range(0, threadCount)
            .Select(thread => Task.Run(() =>
            {
                for (var index = 0; index < eventsPerThread; index++)
                {
                    telemetry.WriteEvent(new { thread, index }, "concurrentEvent");
                    if (index % 50 == 0)
                    {
                        telemetry.Flush();
                    }
                }
            }))
            .ToArray();

        TelemetryContextTests.WithinTimeout(() => Task.WaitAll(workers));
        TelemetryContextTests.WithinTimeout(telemetry.Stop);

        Assert.AreEqual(threadCount * eventsPerThread, writer.Records.Count);
        foreach (var group in writer.Records.GroupBy(record => (int)record.Properties["thread"]!))
        {
            CollectionAssert.AreEqual(
                Enumerable.Range(0, eventsPerThread).ToList(),
                group.Select(record => (int)record.Properties["index"]!).ToList(),
                $"thread {group.Key}'s records should arrive in the order it recorded them");
        }
    }

    [TestMethod]
    public void AWriterExceptionOnlyLosesThatRecordAndDoesNotStopLaterOnes()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new FailingTelemetryWriter(failOperation: "badEvent");
        telemetry.Start(writer);

        telemetry.WriteEvent(new { }, "goodEvent1");
        telemetry.WriteEvent(new { }, "badEvent");
        telemetry.WriteEvent(new { }, "goodEvent2");

        // telemetry is diagnostic - a broken writer mustn't hang flush/stop or throw into the app
        TelemetryContextTests.WithinTimeout(telemetry.Flush);
        CollectionAssert.AreEqual(
            new List<string> { "goodEvent1", "goodEvent2" },
            writer.Written.Select(record => record.Operation).ToList());

        TelemetryContextTests.WithinTimeout(telemetry.Stop);
    }

    [TestMethod]
    public void AStopWhoseFinalWriterFlushFailsStillLeavesTheContextUsable()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        telemetry.Start(new FlushFailingTelemetryWriter());
        telemetry.WriteEvent(new { }, "eventBeforeStop");

        // the failed final flush is reported to whoever called Stop...
        var stop = Task.Run(telemetry.Stop);
        Assert.AreEqual(0, Task.WaitAny([stop], TelemetryContextTests.Timeout), "timed out waiting for Stop");
        Assert.IsInstanceOfType<InvalidOperationException>(stop.Exception?.InnerException);

        // ...but the context is still stopped cleanly - later calls mustn't hang on the
        // already-shut-down adapter, and a replacement writer can be started
        TelemetryContextTests.WithinTimeout(telemetry.Flush);
        TelemetryContextTests.WithinTimeout(telemetry.Stop);

        var replacement = new InMemoryTelemetryWriter();
        telemetry.Start(replacement);
        telemetry.WriteEvent(new { }, "eventAfterRestart");
        TelemetryContextTests.WithinTimeout(telemetry.Stop);

        Assert.AreEqual("eventAfterRestart", replacement.Records.Single().Operation);
    }

    [TestMethod]
    public void PauseDoesNotReturnUntilAWriteInProgressHasFinished()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new ControllableTelemetryWriter { BlockWrites = true };
        telemetry.Start(writer);

        telemetry.WriteEvent(new { }, "slowEvent");
        writer.WaitForWrites(1);

        // the writer is still busy, so pausing (e.g. to time something without telemetry I/O
        // in the way) has to wait for it - a negative check, so it can only wait "a while"
        var pause = Task.Run(telemetry.PauseWriting);
        Assert.IsFalse(pause.Wait(TimeSpan.FromMilliseconds(250)), "pause returned while a write was still in progress");

        writer.BlockWrites = false;
        Assert.IsTrue(pause.Wait(TelemetryContextTests.Timeout), "pause should return once the write finishes");

        telemetry.ResumeWriting();
        TelemetryContextTests.WithinTimeout(telemetry.Stop);
    }

    [TestMethod]
    public void AWriteCannotStartWhileTheWriterIsFlushing()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new ControllableTelemetryWriter { BlockFlushes = true };
        telemetry.Start(writer);

        var flush = Task.Run(telemetry.Flush);
        writer.WaitForFlushes(1);

        // a record arriving while the writer is mid-flush has to wait for the flush to finish -
        // writers (e.g. FileTelemetryWriter's StreamWriter) aren't safe to use concurrently
        telemetry.WriteEvent(new { }, "eventDuringFlush");
        Thread.Sleep(250);
        Assert.AreEqual(0, writer.WritesStarted, "a write started while the writer was still flushing");

        writer.BlockFlushes = false;
        Assert.IsTrue(flush.Wait(TelemetryContextTests.Timeout), "flush should complete once released");
        writer.WaitForWrites(1);

        TelemetryContextTests.WithinTimeout(telemetry.Stop);
    }

    [TestMethod]
    public void StoppingWhilePausedStillWritesEverythingQueued()
    {
        var telemetry = TelemetryContextTests.CreateContext();
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        telemetry.PauseWriting();
        telemetry.WriteEvent(new { }, "pausedEvent");
        TelemetryContextTests.WithinTimeout(telemetry.Stop);

        Assert.AreEqual("pausedEvent", writer.Records.Single().Operation);
    }

    /// <summary>
    /// A uniquely-named source, so no other test's listener can see (or add to) these records.
    /// </summary>
    private static TelemetryContext CreateContext()
        => TelemetryContext.Create($"{nameof(TelemetryContextTests)}.{Guid.NewGuid()}");

    private static void WithinTimeout(Action action)
    {
        var task = Task.Run(action);
        Assert.IsTrue(task.Wait(TelemetryContextTests.Timeout), "timed out - the telemetry consumer may have stalled");
    }

    /// <summary>
    /// Throws from <see cref="Write"/> for one specific operation, and records everything else.
    /// </summary>
    private sealed class FailingTelemetryWriter : ITelemetryWriter
    {
        private readonly string failOperation;
        private readonly List<TelemetryRecord> written = [];

        public FailingTelemetryWriter(string failOperation)
        {
            this.failOperation = failOperation;
        }

        public IReadOnlyList<TelemetryRecord> Written
            => this.written;

        public void Write(TelemetryRecord record)
        {
            if (record.Operation == this.failOperation)
            {
                throw new InvalidOperationException("simulated writer failure");
            }

            this.written.Add(record);
        }

        public void Flush()
        {
        }
    }

    /// <summary>
    /// A writer whose <see cref="Write"/> and/or <see cref="Flush"/> calls can be held "in
    /// progress" until released, and which counts how many of each have started - so a test can
    /// check what the adapter does (or doesn't do) while a writer call is still running.
    /// </summary>
    private sealed class ControllableTelemetryWriter : ITelemetryWriter
    {
        private readonly object sync = new();
        private bool blockWrites;
        private bool blockFlushes;
        private int writesStarted;
        private int flushesStarted;

        public bool BlockWrites
        {
            get => this.Get(() => this.blockWrites);
            set => this.Set(() => this.blockWrites = value);
        }

        public bool BlockFlushes
        {
            get => this.Get(() => this.blockFlushes);
            set => this.Set(() => this.blockFlushes = value);
        }

        public int WritesStarted
            => this.Get(() => this.writesStarted);

        public void Write(TelemetryRecord record)
            => this.Enter(() => this.writesStarted++, () => this.blockWrites);

        public void Flush()
            => this.Enter(() => this.flushesStarted++, () => this.blockFlushes);

        public void WaitForWrites(int count)
            => this.WaitUntil(() => this.writesStarted >= count, "a write to start");

        public void WaitForFlushes(int count)
            => this.WaitUntil(() => this.flushesStarted >= count, "a flush to start");

        private T Get<T>(Func<T> read)
        {
            lock (this.sync)
            {
                return read();
            }
        }

        private void Set(Action write)
        {
            lock (this.sync)
            {
                write();
                Monitor.PulseAll(this.sync);
            }
        }

        private void Enter(Action count, Func<bool> blocked)
        {
            this.Set(count);
            this.WaitUntil(() => !blocked(), "the call to be released");
        }

        private void WaitUntil(Func<bool> condition, string what)
        {
            lock (this.sync)
            {
                var deadline = DateTime.UtcNow + TelemetryContextTests.Timeout;
                while (!condition())
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if ((remaining <= TimeSpan.Zero) || !Monitor.Wait(this.sync, remaining))
                    {
                        throw new TimeoutException($"timed out waiting for {what}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Writes nothing, and throws from every <see cref="Flush"/> - including the final one
    /// <see cref="TelemetryContext.Stop"/> makes.
    /// </summary>
    private sealed class FlushFailingTelemetryWriter : ITelemetryWriter
    {
        public void Write(TelemetryRecord record)
        {
        }

        public void Flush()
            => throw new InvalidOperationException("simulated flush failure");
    }
}
