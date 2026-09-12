// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Records lightweight, opt-in diagnostic telemetry (as opposed to <c>NLog.ILogger</c>-style
/// application logging) - ambient key-value context via <see cref="BeginScope"/>, measured spans
/// via <see cref="BeginTimer"/>, and one-off point-in-time facts via <see cref="WriteEvent"/>.
/// Built on <see cref="System.Diagnostics.Activity"/> rather than a bespoke implementation, so it
/// gets ambient async-flowing context, parent/child nesting, and (critically) a genuinely free
/// no-op path when nothing is listening: without calling <see cref="Start"/>, every method here
/// still runs (so callers don't need to guard call sites), but
/// <see cref="ActivitySource.StartActivity(string)"/> returns <see langword="null"/> immediately
/// and nothing further happens. A plain instance class - no ambient/global state of its own, so
/// it's easy to construct more than one (e.g. in tests) - see <see cref="Telemetry"/> for the
/// process-wide ambient instance most call sites actually use.
/// </summary>
public sealed class TelemetryContext
{
    /// <summary>
    /// Tag name distinguishing <see cref="BeginScope"/>/<see cref="BeginTimer"/>/<see cref="WriteEvent"/>
    /// activities from each other in <see cref="TelemetryAdapter"/>'s output - set once, here,
    /// rather than left as a convention callers could get wrong.
    /// </summary>
    internal const string KindTag = "telemetry.kind";

    private readonly ActivitySource activitySource;

    private TelemetryAdapter? adapter;

    private TelemetryContext(ActivitySource activitySource)
    {
        this.activitySource = activitySource;
    }

    /// <summary>
    /// Creates a new <see cref="TelemetryContext"/> backed by its own <see cref="ActivitySource"/>
    /// named <paramref name="sourceName"/> - call <see cref="Start"/> to actually route anything
    /// it records anywhere.
    /// </summary>
    public static TelemetryContext Create(string sourceName)
        => new(new ActivitySource(sourceName));

    /// <summary>
    /// Starts routing everything this context records to <paramref name="writer"/>. Safe to call
    /// again later (with the same or a different writer) - e.g. reacting to a runtime config
    /// toggle without restarting the app - implicitly stopping whatever was previously running
    /// first.
    /// </summary>
    public void Start(ITelemetryWriter writer)
    {
        this.Stop();
        this.adapter = TelemetryAdapter.Start(this.activitySource, writer);
    }

    /// <summary>
    /// Blocks until every record recorded before this call has reached the writer, then flushes
    /// the writer itself. A no-op if <see cref="Start"/> hasn't been called. <see cref="Stop"/>
    /// already does this as part of shutting down - call <see cref="Flush"/> explicitly when you
    /// want the guarantee without also stopping collection, e.g. before inspecting an
    /// <see cref="InMemoryTelemetryWriter"/>'s <see cref="InMemoryTelemetryWriter.Records"/> mid-test.
    /// </summary>
    public void Flush()
        => this.adapter?.Flush();

    /// <summary>
    /// Stops the writer from doing any actual work (writing, flushing) until <see cref="ResumeWriting"/>
    /// - collection is untouched, so every <see cref="BeginScope"/>/<see cref="BeginTimer"/>/
    /// <see cref="WriteEvent"/> call site keeps firing and queuing exactly as normal, just
    /// backing up instead of reaching the writer. A no-op if <see cref="Start"/> hasn't been
    /// called. For ruling out the writer's own work (e.g. disk I/O) as a source of latency around
    /// a hot path, without losing what happened during it - see <see cref="TelemetryAdapter.PauseWriting"/>.
    /// </summary>
    public void PauseWriting()
        => this.adapter?.PauseWriting();

    /// <summary>
    /// Undoes <see cref="PauseWriting"/>.
    /// </summary>
    public void ResumeWriting()
        => this.adapter?.ResumeWriting();

    /// <summary>
    /// Stops routing anywhere - <see cref="BeginScope"/>/<see cref="BeginTimer"/>/
    /// <see cref="WriteEvent"/> keep working but go nowhere (see the class remarks). Blocks until
    /// any already-queued records have been flushed to the previous writer.
    /// </summary>
    public void Stop()
    {
        this.adapter?.Dispose();
        this.adapter = null;
    }

    /// <summary>
    /// Adds ambient key-value context for anything recorded (by this context, on this async
    /// flow) while the returned scope is open. Records no duration and writes nothing for the
    /// scope itself - e.g. wrap a whole activation in
    /// <c>BeginScope(new { activation = id })</c> so every timer/event inside it automatically
    /// carries that, without threading it through every method signature.
    /// </summary>
    public TelemetryScope BeginScope(object properties, [CallerMemberName] string caller = "")
    {
        var activity = this.activitySource.StartActivity(caller);
        TelemetryContext.ApplyProperties(activity, properties);
        activity?.SetTag(TelemetryContext.KindTag, "scope");
        return new TelemetryScope(activity);
    }

    /// <summary>
    /// Measures a span of work - starts timing now, and when the returned
    /// <see cref="TelemetryTimer"/> is disposed, its duration becomes final and (if
    /// <see cref="Start"/> has been called) gets written out, tagged with
    /// <paramref name="properties"/> plus anything ambient from any enclosing
    /// <see cref="BeginScope"/>.
    /// </summary>
    public TelemetryTimer BeginTimer(object properties, [CallerMemberName] string caller = "")
    {
        var activity = this.activitySource.StartActivity(caller);
        TelemetryContext.ApplyProperties(activity, properties);
        activity?.SetTag(TelemetryContext.KindTag, "timer");
        return new TelemetryTimer(activity);
    }

    /// <summary>
    /// Records a single point-in-time fact - no duration, no scope opened - but still carries
    /// whatever's currently ambient from any enclosing <see cref="BeginScope"/>/<see cref="BeginTimer"/>.
    /// </summary>
    public void WriteEvent(object properties, [CallerMemberName] string caller = "")
    {
        using var activity = this.activitySource.StartActivity(caller);
        TelemetryContext.ApplyProperties(activity, properties);
        activity?.SetTag(TelemetryContext.KindTag, "event");
    }

    /// <summary>
    /// Copies <paramref name="properties"/>'s own public instance properties onto
    /// <paramref name="activity"/> as tags - reflection-based, but only ever runs when
    /// <paramref name="activity"/> is non-null, i.e. only when something is actually listening
    /// (see the class remarks) - so this cost is never paid in the common case where telemetry
    /// is disabled.
    /// </summary>
    private static void ApplyProperties(Activity? activity, object properties)
    {
        if (activity is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(properties);

        foreach (var property in properties.GetType().GetProperties())
        {
            activity.SetTag(property.Name, property.GetValue(properties));
        }
    }
}
