// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.CmdPal.JsonRpc.Models;

internal sealed partial class JSWeakReferenceRegistry<TKey, TTarget>
    where TKey : notnull
    where TTarget : class
{
    private readonly ConcurrentDictionary<TKey, List<WeakReference<TTarget>>> _targets = new();

    internal void Register(TKey key, TTarget target)
    {
        while (true)
        {
            var targets = _targets.GetOrAdd(key, static _ => []);
            lock (targets)
            {
                if (!IsCurrent(key, targets))
                {
                    continue;
                }

                targets.RemoveAll(reference => !reference.TryGetTarget(out _));
                if (!targets.Exists(reference =>
                    reference.TryGetTarget(out var current) && ReferenceEquals(current, target)))
                {
                    targets.Add(new WeakReference<TTarget>(target));
                }

                return;
            }
        }
    }

    internal void Unregister(TKey key, TTarget target)
    {
        if (!_targets.TryGetValue(key, out var targets))
        {
            return;
        }

        lock (targets)
        {
            if (!IsCurrent(key, targets))
            {
                return;
            }

            targets.RemoveAll(reference =>
                !reference.TryGetTarget(out var current) || ReferenceEquals(current, target));
            RemoveIfEmpty(key, targets);
        }
    }

    internal List<TTarget> GetLiveTargets(TKey key)
    {
        while (_targets.TryGetValue(key, out var targets))
        {
            lock (targets)
            {
                if (!IsCurrent(key, targets))
                {
                    continue;
                }

                var liveTargets = new List<TTarget>(targets.Count);
                targets.RemoveAll(reference => !reference.TryGetTarget(out _));
                foreach (var reference in targets)
                {
                    if (reference.TryGetTarget(out var target))
                    {
                        liveTargets.Add(target);
                    }
                }

                RemoveIfEmpty(key, targets);
                return liveTargets;
            }
        }

        return [];
    }

    internal int GetRegistrationCount(TKey key)
    {
        while (_targets.TryGetValue(key, out var targets))
        {
            lock (targets)
            {
                if (!IsCurrent(key, targets))
                {
                    continue;
                }

                return targets.Count;
            }
        }

        return 0;
    }

    private bool IsCurrent(TKey key, List<WeakReference<TTarget>> targets)
    {
        return _targets.TryGetValue(key, out var currentTargets) &&
            ReferenceEquals(targets, currentTargets);
    }

    private void RemoveIfEmpty(TKey key, List<WeakReference<TTarget>> targets)
    {
        if (targets.Count == 0)
        {
            ((ICollection<KeyValuePair<TKey, List<WeakReference<TTarget>>>>)_targets)
                .Remove(new KeyValuePair<TKey, List<WeakReference<TTarget>>>(key, targets));
        }
    }
}
