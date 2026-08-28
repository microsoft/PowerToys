// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Owns content observers independently of their UI containers. Snapshots retain
/// unchanged models by reference, and only the UI scheduler mutates Items.
/// </summary>
public sealed partial class ContentCollectionViewModel : ExtensionObjectViewModel
{
    private readonly Lock _gate = new();
    private readonly Func<IContent, WeakReference<IPageContext>, ContentViewModel?> _factory;
    private Dictionary<IContent, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private int _requestVersion;
    private int _publishedVersion;
    private bool _stopped;

    public ObservableCollection<ContentViewModel> Items { get; } = [];

    public event EventHandler? Updated;

    public ContentCollectionViewModel(
        WeakReference<IPageContext> context,
        Func<IContent, WeakReference<IPageContext>, ContentViewModel?>? factory = null)
        : base(context)
    {
        _factory = factory ?? ContentViewModelFactory.Create;
    }

    public override void InitializeProperties()
    {
    }

    public void Update(IEnumerable<IContent>? models, bool focusSoleContent = false)
    {
        Dictionary<IContent, Entry> previous;
        int request;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            request = ++_requestVersion;
            previous = new(_entries, ReferenceEqualityComparer.Instance);
            foreach (var entry in previous.Values)
            {
                entry.Retain();
            }
        }

        var created = new List<Entry>();
        var next = new Dictionary<IContent, Entry>(ReferenceEqualityComparer.Instance);
        var ordered = new List<ContentViewModel>();
        try
        {
            foreach (var model in models ?? [])
            {
                if (model is null)
                {
                    continue;
                }

                if (!next.TryGetValue(model, out var entry))
                {
                    if (!previous.TryGetValue(model, out entry))
                    {
                        var viewModel = _factory(model, PageContext);
                        if (viewModel is null)
                        {
                            continue;
                        }

                        entry = new(viewModel);
                        created.Add(entry);
                        viewModel.InitializeProperties();
                    }

                    next.Add(model, entry);
                }

                ordered.Add(entry.ViewModel);
            }

            Dictionary<IContent, Entry> removed;
            int published;
            lock (_gate)
            {
                if (_stopped || request != _requestVersion)
                {
                    return;
                }

                foreach (var entry in next.Values)
                {
                    entry.Retain();
                }

                removed = _entries;
                _entries = next;
                published = ++_publishedVersion;
            }

            foreach (var entry in removed.Values)
            {
                entry.Release();
            }

            if (!TryDoOnUiThread(() =>
            {
                if (Volatile.Read(ref _stopped) || published != Volatile.Read(ref _publishedVersion))
                {
                    return;
                }

                foreach (var item in ordered)
                {
                    item.OnlyControlOnPage = focusSoleContent && ordered.Count == 1;
                }

                ListHelpers.InPlaceUpdateList(Items, ordered);
                Updated?.Invoke(this, EventArgs.Empty);
            }))
            {
                SafeCleanup();
            }
        }
        finally
        {
            // Leases keep reused observers alive while concurrent snapshots are built.
            // No extension getter or event revocation occurs while holding _gate.
            foreach (var entry in previous.Values)
            {
                entry.Release();
            }

            foreach (var entry in created)
            {
                entry.Release();
            }
        }
    }

    protected override void UnsafeCleanup()
    {
        Dictionary<IContent, Entry> removed;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            ++_requestVersion;
            ++_publishedVersion;
            removed = _entries;
            _entries = new(ReferenceEqualityComparer.Instance);
        }

        foreach (var entry in removed.Values)
        {
            entry.Release();
        }

        DoOnUiThread(() => Items.Clear());
    }

    private sealed class Entry(ContentViewModel viewModel)
    {
        private int _references = 1;

        public ContentViewModel ViewModel { get; } = viewModel;

        public void Retain() => Interlocked.Increment(ref _references);

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                ViewModel.SafeCleanup();
            }
        }
    }
}
