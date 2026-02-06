// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels;

internal sealed class ViewModelCache<TModel, TVm>
    where TModel : class
    where TVm : class
{
    private sealed class Entry
    {
        public required TVm Vm { get; set; }

        public int LastSeenGeneration { get; set; }
    }

    private readonly ICacheKeyProvider<TModel> _keys;
    private readonly Func<TModel, TVm> _factory;
    private readonly Action<TVm, TModel>? _rebind;
    private readonly Action<TVm>? _onEvict;

    private readonly Dictionary<CacheKey, Entry> _entries = [];
    private int _generation;

    public ViewModelCache(
        ICacheKeyProvider<TModel> keys,
        Func<TModel, TVm> factory,
        Action<TVm, TModel>? rebind = null,
        Action<TVm>? onEvict = null)
    {
        _keys = keys;
        _factory = factory;
        _rebind = rebind;
        _onEvict = onEvict;
    }

    public int BeginGeneration() => ++_generation;

    public TVm GetOrCreate(TModel model, out bool created)
    {
        var key = _keys.GetKey(model);

        if (_entries.TryGetValue(key, out var entry))
        {
            created = false;
            entry.LastSeenGeneration = _generation;
            _rebind?.Invoke(entry.Vm, model);
            return entry.Vm;
        }

        created = true;
        var vm = _factory(model);
        _entries[key] = new Entry { Vm = vm, LastSeenGeneration = _generation };
        return vm;
    }

    public void Remove(TModel model)
    {
        var key = _keys.GetKey(model);
        if (_entries.Remove(key, out var entry))
        {
            _onEvict?.Invoke(entry.Vm);
        }
    }

    public List<TVm> EvictNotSeenFor(int keepGenerations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keepGenerations, 0);

        var threshold = _generation - keepGenerations;
        List<CacheKey>? toRemove = null;
        var evicted = new List<TVm>();

        foreach (var (key, entry) in _entries)
        {
            if (entry.LastSeenGeneration <= threshold)
            {
                toRemove ??= new List<CacheKey>();
                toRemove.Add(key);
                _onEvict?.Invoke(entry.Vm);
                evicted.Add(entry.Vm);
            }
        }

        if (toRemove is not null)
        {
            foreach (var key in toRemove)
            {
                _entries.Remove(key);
            }
        }

        return evicted;
    }

    public List<TVm> Clear()
    {
        var evicted = new List<TVm>(_entries.Count);
        foreach (var entry in _entries.Values)
        {
            _onEvict?.Invoke(entry.Vm);
            evicted.Add(entry.Vm);
        }

        _entries.Clear();
        return evicted;
    }

    public int Count => _entries.Count;
}
