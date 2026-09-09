// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Owns the CPU measurement interval and publishes one snapshot to all active views.
/// </summary>
internal sealed partial class CpuSampler : IDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly Func<ICpuSampleSource> _sourceFactory;
    private readonly TimeProvider _timeProvider;
    private readonly UsageHistory _history;
    private readonly Action<Exception> _logFailure;
    private readonly List<Subscription> _subscriptions = [];
    private ICpuSampleSource? _source;
    private Session? _session;
    private CpuSnapshot _snapshot = CpuSnapshot.Empty;
    private bool _sampling;
    private bool _disposed;
    private bool _failureLogged;

    public CpuSampler(Func<ICpuSampleSource> sourceFactory, TimeProvider? timeProvider = null, Action<Exception>? logFailure = null)
    {
        _sourceFactory = sourceFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _history = new UsageHistory(seriesCount: 2, _timeProvider);
        _logFailure = logFailure ?? (static ex => CoreLogger.LogError("Failed to sample or publish CPU performance data.", ex));
    }

    public CpuSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public IDisposable Subscribe(Action updated, bool includeTopProcesses = false)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var subscription = new Subscription(this, updated, includeTopProcesses);
            _subscriptions.Add(subscription);
            if (_session is null)
            {
                var session = new Session();
                _session = session;
                try
                {
                    // Prime on the timer thread, then measure full one-second intervals.
                    session.Timer = _timeProvider.CreateTimer(Tick, session, TimeSpan.Zero, Interval);
                }
                catch
                {
                    _session = null;
                    _subscriptions.Remove(subscription);
                    throw;
                }
            }

            return subscription;
        }
    }

    private void Tick(object? state)
    {
        var session = (Session)state!;
        bool includeTopProcesses = false;
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session) || _sampling)
            {
                return;
            }

            _sampling = true;
            foreach (var subscription in _subscriptions)
            {
                includeTopProcesses |= subscription.IncludeTopProcesses;
            }
        }

        try
        {
            _source ??= _sourceFactory();
            if (session.NeedsBaseline)
            {
                // Never average a new reading over the period with no active views.
                _source.ResetSamplingInterval();
                lock (_gate)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        session.NeedsBaseline = false;
                        session.Timer!.Change(Interval, Interval);
                    }
                }

                return;
            }

            var sample = _source.Sample(includeTopProcesses);
            Subscription[] subscriptions;
            lock (_gate)
            {
                if (!ReferenceEquals(_session, session))
                {
                    return;
                }

                _history.Add(sample.CpuUsage * 100, sample.KernelUsage * 100);
                var snapshot = new CpuSnapshot(sample.CpuUsage, sample.KernelUsage, sample.CpuSpeed, _history.GetSnapshot());
                Volatile.Write(ref _snapshot, snapshot);
                subscriptions = [.. _subscriptions];
            }

            // Consumers may stop or dispose views here; do not hold the sampler lock.
            foreach (var subscription in subscriptions)
            {
                try
                {
                    subscription.Notify();
                }
                catch (Exception ex)
                {
                    LogFailure(ex);
                }
            }
        }
        catch (Exception ex)
        {
            session.NeedsBaseline = true;
            LogFailure(ex);
        }
        finally
        {
            lock (_gate)
            {
                _sampling = false;
            }
        }
    }

    private void LogFailure(Exception ex)
    {
        if (!_failureLogged)
        {
            _failureLogged = true;
            _logFailure(ex);
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
            if (_subscriptions.Count == 0)
            {
                var session = _session;
                _session = null;
                session?.Timer?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var subscription in _subscriptions)
            {
                subscription.Deactivate();
            }

            _subscriptions.Clear();
            var session = _session;
            _session = null;
            session?.Timer?.Dispose();
        }
    }

    private sealed class Session
    {
        public ITimer? Timer { get; set; }

        public bool NeedsBaseline { get; set; } = true;
    }

    private sealed partial class Subscription(CpuSampler owner, Action updated, bool includeTopProcesses) : IDisposable
    {
        private int _disposed;

        public bool IncludeTopProcesses { get; } = includeTopProcesses;

        public void Notify()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                updated();
            }
        }

        public void Deactivate() => Interlocked.Exchange(ref _disposed, 1);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unsubscribe(this);
            }
        }
    }
}
