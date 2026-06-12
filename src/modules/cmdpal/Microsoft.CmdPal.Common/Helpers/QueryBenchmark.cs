// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Lightweight benchmarking service for CmdPal query pipeline latency.
/// Enable via <see cref="IsEnabled"/>. Disabled by default in production.
/// Results are written to the CmdPal logger and can be retrieved programmatically.
/// </summary>
public sealed class QueryBenchmark
{
    public static QueryBenchmark Instance { get; } = new();

    /// <summary>
    /// Set to true to enable benchmark instrumentation. Default is false.
    /// When disabled, all methods are effectively no-ops.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Maximum number of completed sessions to retain for reporting.
    /// </summary>
    private const int MaxRetainedSessions = 64;

    private readonly ConcurrentQueue<BenchmarkSession> _completedSessions = new();

    private QueryBenchmark()
    {
    }

    /// <summary>
    /// Starts a new benchmark session for a query. Returns a session handle
    /// that must be completed via <see cref="BenchmarkSession.Complete"/>.
    /// If benchmarking is disabled, returns a no-op session.
    /// </summary>
    public BenchmarkSession StartSession(string query, string scenario)
    {
        if (!IsEnabled)
        {
            return BenchmarkSession.NoOp;
        }

        return new BenchmarkSession(query, scenario, this);
    }

    /// <summary>
    /// Gets a snapshot of all completed benchmark sessions.
    /// </summary>
    public BenchmarkSession[] GetCompletedSessions() => [.. _completedSessions];

    /// <summary>
    /// Clears all retained sessions.
    /// </summary>
    public void Clear() => _completedSessions.Clear();

    /// <summary>
    /// Generates a summary report of all completed sessions.
    /// </summary>
    public string GenerateReport()
    {
        var sessions = GetCompletedSessions();
        if (sessions.Length == 0)
        {
            return "[QueryBenchmark] No sessions recorded.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("============================================================");
        sb.AppendLine("[QueryBenchmark] Latency Report");
        sb.AppendLine("============================================================");

        foreach (var session in sessions)
        {
            sb.AppendLine(session.ToReportString());
            sb.AppendLine();
        }

        // Summary stats
        var queryGroups = sessions.GroupBy(s => s.Scenario);
        sb.AppendLine("--- Aggregates ---");
        foreach (var group in queryGroups)
        {
            var totalTimes = group.Select(s => s.TotalElapsedMs).Where(t => t >= 0).ToArray();
            var firstResultTimes = group.Select(s => s.TimeToFirstResultMs).Where(t => t >= 0).ToArray();

            sb.AppendLine($"  Scenario: {group.Key} ({totalTimes.Length} samples)");
            if (totalTimes.Length > 0)
            {
                sb.AppendLine($"    Total time    — avg: {totalTimes.Average():F1}ms, min: {totalTimes.Min():F1}ms, max: {totalTimes.Max():F1}ms, p50: {Percentile(totalTimes, 50):F1}ms, p95: {Percentile(totalTimes, 95):F1}ms");
            }

            if (firstResultTimes.Length > 0)
            {
                sb.AppendLine($"    First result  — avg: {firstResultTimes.Average():F1}ms, min: {firstResultTimes.Min():F1}ms, max: {firstResultTimes.Max():F1}ms, p50: {Percentile(firstResultTimes, 50):F1}ms, p95: {Percentile(firstResultTimes, 95):F1}ms");
            }
        }

        sb.AppendLine("============================================================");
        return sb.ToString();
    }

    internal void RecordSession(BenchmarkSession session)
    {
        _completedSessions.Enqueue(session);

        // Trim to max
        while (_completedSessions.Count > MaxRetainedSessions)
        {
            _completedSessions.TryDequeue(out _);
        }

        // Log to CmdPal logger
        CoreLogger.LogInfo($"[Benchmark] {session.ToSummaryString()}");
    }

    private static double Percentile(double[] sortedValues, int percentile)
    {
        var sorted = sortedValues.OrderBy(v => v).ToArray();
        var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

/// <summary>
/// Represents a single benchmark measurement session for a query operation.
/// Thread-safe for recording milestones from multiple threads.
/// </summary>
public sealed class BenchmarkSession
{
    internal static readonly BenchmarkSession NoOp = new();

    private readonly Stopwatch? _stopwatch;
    private readonly QueryBenchmark? _owner;
    private readonly ConcurrentDictionary<string, double> _extensionTimings = new();
    private readonly ConcurrentBag<(string Name, double ElapsedMs)> _milestones = [];
    private long _firstResultTicks = -1;
    private bool _isCompleted;

    /// <summary>The query text being benchmarked.</summary>
    public string Query { get; } = string.Empty;

    /// <summary>The scenario name (e.g., "ColdStart", "WarmQuery", "RapidTyping").</summary>
    public string Scenario { get; } = string.Empty;

    /// <summary>Total elapsed time in ms, or -1 if not yet completed.</summary>
    public double TotalElapsedMs => _stopwatch?.Elapsed.TotalMilliseconds ?? -1;

    /// <summary>Time to first result in ms, or -1 if no first result was recorded.</summary>
    public double TimeToFirstResultMs
    {
        get
        {
            var ticks = Interlocked.Read(ref _firstResultTicks);
            if (ticks < 0 || _stopwatch == null)
            {
                return -1;
            }

            return (ticks / (double)Stopwatch.Frequency) * 1000.0;
        }
    }

    /// <summary>Per-extension timing data.</summary>
    public IReadOnlyDictionary<string, double> ExtensionTimings => _extensionTimings;

    /// <summary>Named milestones recorded during the session.</summary>
    public IReadOnlyCollection<(string Name, double ElapsedMs)> Milestones => _milestones;

    // No-op constructor
    private BenchmarkSession()
    {
    }

    internal BenchmarkSession(string query, string scenario, QueryBenchmark owner)
    {
        Query = query;
        Scenario = scenario;
        _owner = owner;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records the point at which the first result was available to the UI.
    /// Only the first call has effect; subsequent calls are ignored.
    /// </summary>
    public void MarkFirstResult()
    {
        if (_stopwatch == null)
        {
            return;
        }

        Interlocked.CompareExchange(ref _firstResultTicks, _stopwatch.ElapsedTicks, -1);
    }

    /// <summary>
    /// Records a named milestone with the current elapsed time.
    /// </summary>
    public void RecordMilestone(string name)
    {
        if (_stopwatch == null)
        {
            return;
        }

        _milestones.Add((name, _stopwatch.Elapsed.TotalMilliseconds));
    }

    /// <summary>
    /// Records the time taken by a specific extension/provider.
    /// </summary>
    public void RecordExtensionTime(string extensionName, double elapsedMs)
    {
        if (_stopwatch == null)
        {
            return;
        }

        _extensionTimings[extensionName] = elapsedMs;
    }

    /// <summary>
    /// Starts a scoped timer for an extension. Dispose the returned handle to record.
    /// </summary>
    public ExtensionTimer TimeExtension(string extensionName)
    {
        if (_stopwatch == null)
        {
            return ExtensionTimer.NoOp;
        }

        return new ExtensionTimer(this, extensionName);
    }

    /// <summary>
    /// Completes the benchmark session and records it.
    /// </summary>
    public void Complete()
    {
        if (_stopwatch == null || _isCompleted)
        {
            return;
        }

        _isCompleted = true;
        _stopwatch.Stop();
        _owner?.RecordSession(this);
    }

    internal string ToSummaryString()
    {
        var firstResult = TimeToFirstResultMs >= 0 ? $"{TimeToFirstResultMs:F1}ms" : "N/A";
        return $"[{Scenario}] Query='{Query}' Total={TotalElapsedMs:F1}ms FirstResult={firstResult} Extensions={_extensionTimings.Count}";
    }

    internal string ToReportString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  [{Scenario}] Query: '{Query}'");
        sb.AppendLine($"    Total: {TotalElapsedMs:F1} ms");
        sb.AppendLine($"    First result: {(TimeToFirstResultMs >= 0 ? $"{TimeToFirstResultMs:F1} ms" : "N/A")}");

        if (_milestones.Count > 0)
        {
            sb.AppendLine("    Milestones:");
            foreach (var (name, elapsed) in _milestones.OrderBy(m => m.ElapsedMs))
            {
                sb.AppendLine($"      {name}: {elapsed:F1} ms");
            }
        }

        if (_extensionTimings.Count > 0)
        {
            sb.AppendLine("    Extensions:");
            foreach (var (name, elapsed) in _extensionTimings.OrderByDescending(kvp => kvp.Value))
            {
                sb.AppendLine($"      {name}: {elapsed:F1} ms");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Disposable timer for measuring a single extension's execution time.
/// </summary>
public readonly struct ExtensionTimer : IDisposable
{
    internal static readonly ExtensionTimer NoOp = default;

    private readonly BenchmarkSession? _session;
    private readonly string? _extensionName;
    private readonly long _startTicks;

    internal ExtensionTimer(BenchmarkSession session, string extensionName)
    {
        _session = session;
        _extensionName = extensionName;
        _startTicks = Stopwatch.GetTimestamp();
    }

    public void Dispose()
    {
        if (_session == null || _extensionName == null)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_startTicks);
        _session.RecordExtensionTime(_extensionName, elapsed.TotalMilliseconds);
    }
}
