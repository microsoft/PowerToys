// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> of <see cref="TopLevelViewModel"/>
/// that supports replacing the entire contents atomically with a single
/// <see cref="NotifyCollectionChangedAction.Reset"/> notification.
///
/// <para>
/// Using <see cref="ObservableCollection{T}"/>'s built-in Add/Remove/Insert
/// mutations (or helpers like <c>ListHelpers.InPlaceUpdateList</c>) fires one
/// <see cref="INotifyCollectionChanged.CollectionChanged"/> event per item
/// mutation. The dock subscribes to that event and does a full rebuild for
/// each, so a single provider reload (which can churn dozens of band entries)
/// turns into dozens of full dock rebuilds.
/// </para>
///
/// <para>
/// <see cref="ReplaceWith"/> bypasses the per-item notifications by mutating
/// the protected <see cref="Collection{T}.Items"/> list directly and then
/// raising one <c>Reset</c> at the end.
/// </para>
/// </summary>
public sealed partial class DockBandsCollection : ObservableCollection<TopLevelViewModel>
{
    /// <summary>
    /// Replaces the contents of this collection with <paramref name="newItems"/>
    /// and raises exactly one <see cref="NotifyCollectionChangedAction.Reset"/>
    /// event (plus the standard <c>Count</c> / <c>Item[]</c> property change
    /// notifications). If the new contents are reference-equal to the current
    /// contents, no notification is raised.
    /// </summary>
    public void ReplaceWith(IEnumerable<TopLevelViewModel> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);

        // Materialize once so we can compare and iterate without re-enumerating.
        var snapshot = newItems as IList<TopLevelViewModel> ?? [.. newItems];

        // Cheap short-circuit: same length and same instances in the same
        // order means there is nothing to broadcast.
        if (SequenceReferenceEquals(Items, snapshot))
        {
            return;
        }

        Items.Clear();
        for (var i = 0; i < snapshot.Count; i++)
        {
            Items.Add(snapshot[i]);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static bool SequenceReferenceEquals(IList<TopLevelViewModel> a, IList<TopLevelViewModel> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
