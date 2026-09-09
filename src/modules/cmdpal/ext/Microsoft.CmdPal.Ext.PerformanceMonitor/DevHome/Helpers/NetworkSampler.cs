// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Owns one network polling interval for all pages and dock bands using the same counters.
/// </summary>
internal sealed partial class NetworkSampler(NetworkStats source, TimeProvider timeProvider) : IDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = [];
    private Session? _session;
    private bool _sampling;
    private bool _disposed;
    private bool _failureLogged;

    public IDisposable Subscribe(Action updated)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var subscription = new Subscription(this, updated);
            _subscriptions.Add(subscription);
            if (_session is null)
            {
                var session = new Session();
                _session = session;
                try
                {
                    session.Timer = timeProvider.CreateTimer(Tick, session, TimeSpan.Zero, Interval);
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
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session) || _sampling)
            {
                return;
            }

            _sampling = true;
        }

        try
        {
            // Keep native reads outside the lifecycle lock so stopping a view
            // never waits for a counter read. Publish only into the active session.
            var observation = source.ReadObservation();
            Subscription[] subscriptions;
            lock (_gate)
            {
                if (!ReferenceEquals(_session, session))
                {
                    return;
                }

                if (session.NeedsBaseline)
                {
                    source.ResetSamplingInterval(observation);
                    session.NeedsBaseline = false;
                    session.Timer!.Change(Interval, Interval);
                    return;
                }

                source.ApplyObservation(observation);
                subscriptions = [.. _subscriptions];
            }

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
            CoreLogger.LogError("Failed to sample or publish network performance data.", ex);
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

    private sealed partial class Subscription(NetworkSampler owner, Action updated) : IDisposable
    {
        private int _disposed;

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
