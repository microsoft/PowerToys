// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Channels;

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Bridges a <see cref="TelemetryContext"/>'s recorded <see cref="Activity"/> instances to an
/// <see cref="ITelemetryWriter"/> - the machinery every writer would otherwise have to
/// reimplement for itself: flattening a finished activity's parent chain into one
/// <see cref="TelemetryRecord"/> (see <see cref="Flatten"/>), and keeping that off the caller's
/// own thread via a background-drained <see cref="Channel{T}"/>, so a slow writer (disk I/O)
/// never adds latency to whatever code just finished a <see cref="TelemetryTimer"/> or
/// <see cref="TelemetryScope"/>. The channel carries <see cref="Action"/>s rather than
/// <see cref="TelemetryRecord"/>s directly so <see cref="Flush"/> can enqueue its own completion
/// signal and rely on the channel's FIFO, single-reader ordering to know every record queued
/// ahead of it has already reached the writer. Internal - <see cref="TelemetryContext.Start"/> is
/// the public entry point.
/// </summary>
internal sealed class TelemetryAdapter : IDisposable
{
    private readonly ActivityListener listener;
    private readonly Channel<Action> channel;
    private readonly Task consumerTask;
    private readonly ITelemetryWriter writer;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly object pauseLock = new();
    private bool writingPaused;

    private TelemetryAdapter(ActivitySource activitySource, ITelemetryWriter writer)
    {
        this.writer = writer;

        this.channel = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        this.listener = new ActivityListener
        {
            ShouldListenTo = source => source == activitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var record = TelemetryAdapter.Flatten(activity);
                _ = this.channel.Writer.TryWrite(() => writer.Write(record));
            },
        };
        ActivitySource.AddActivityListener(this.listener);

        this.consumerTask = Task.Run(async () =>
        {
            await foreach (var action in this.channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // blocks here while paused - the channel (already unbounded) keeps accepting
                // and buffering new records regardless, so collection never stalls, only the
                // writer's own work does
                await this.writeGate.WaitAsync().ConfigureAwait(false);
                this.writeGate.Release();
                action();
            }
        });
    }

    public static TelemetryAdapter Start(ActivitySource activitySource, ITelemetryWriter writer)
        => new(activitySource, writer);

    /// <summary>
    /// Stops the consumer from calling <see cref="ITelemetryWriter.Write"/> (or flushing) until
    /// <see cref="ResumeWriting"/> - collection is untouched, so every <see cref="TelemetryTimer"/>/
    /// <see cref="TelemetryScope"/>/<see cref="TelemetryContext.WriteEvent"/> still fires and
    /// queues normally, just backing up in the channel instead of reaching the writer. Idempotent.
    /// </summary>
    public void PauseWriting()
    {
        lock (this.pauseLock)
        {
            if (!this.writingPaused)
            {
                this.writingPaused = true;
                this.writeGate.Wait();
            }
        }
    }

    /// <summary>
    /// Undoes <see cref="PauseWriting"/> - the consumer resumes draining whatever backed up while
    /// paused. Idempotent.
    /// </summary>
    public void ResumeWriting()
    {
        lock (this.pauseLock)
        {
            if (this.writingPaused)
            {
                this.writingPaused = false;
                _ = this.writeGate.Release();
            }
        }
    }

    /// <summary>
    /// Blocks until every record enqueued before this call has reached <see cref="ITelemetryWriter.Write"/>,
    /// then flushes the writer itself - unlike <see cref="Dispose"/>, collection keeps running
    /// afterwards. If writing is currently paused, this blocks until <see cref="ResumeWriting"/>
    /// is called - flushing "nothing has been written yet" isn't a coherent thing to wait for.
    /// </summary>
    public void Flush()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = this.channel.Writer.TryWrite(() => signal.SetResult());
        signal.Task.GetAwaiter().GetResult();
        this.writer.Flush();
    }

    /// <summary>
    /// Stops accepting new activity, then blocks until every already-queued record has been
    /// handed to the writer, and flushes the writer itself (same guarantee as <see cref="Flush"/>,
    /// duplicated here rather than shared - a normal shutdown doesn't silently drop whatever was
    /// still buffered, whether or not something already called <see cref="Flush"/> first).
    /// Force-resumes writing first if it was paused - otherwise shutdown would deadlock waiting
    /// for a consumer that's blocked waiting for a resume that's never coming.
    /// </summary>
    public void Dispose()
    {
        this.listener.Dispose();
        this.channel.Writer.Complete();
        this.ResumeWriting();
        this.consumerTask.GetAwaiter().GetResult();
        this.writer.Flush();
    }

    /// <summary>
    /// Walks <paramref name="activity"/>'s own <see cref="Activity.Parent"/> chain up to the
    /// root, merging every ancestor's tags into one flat set - root-most first, so a more
    /// specific (inner) scope's own value wins on key collision. <see cref="Activity"/> tags
    /// aren't inherited from parent to child on their own, so without this, context added by an
    /// outer <see cref="TelemetryContext.BeginScope"/> would never reach anything recorded
    /// inside it.
    /// </summary>
    private static TelemetryRecord Flatten(Activity activity)
    {
        var chain = new List<Activity>();
        for (var current = (Activity?)activity; current is not null; current = current.Parent)
        {
            chain.Add(current);
        }

        var properties = new Dictionary<string, object?>();
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            foreach (var tag in chain[i].TagObjects)
            {
                properties[tag.Key] = tag.Value;
            }
        }

        _ = properties.Remove(TelemetryContext.KindTag, out var kind);

        return new TelemetryRecord(
            activity.StartTimeUtc,
            activity.OperationName,
            (string)kind!,
            activity.Duration,
            activity.SpanId.ToString(),
            activity.Parent?.SpanId.ToString(),
            properties);
    }
}
