// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TopToolbar.Models;
using TopToolbar.Providers;

namespace TopToolbar.Stores
{
    /// <summary>
    /// Central state store for toolbar groups.  Phase 1: supports wholesale replacement of a single
    /// dynamic provider group (WorkspaceProvider) plus future static config groups merged externally.
    /// Phase 2 will move UI to data binding directly on this store.
    /// </summary>
    public class ToolbarStore
    {
        private readonly object _gate = new();

        /// <summary>
        /// Gets the ordered collection of groups (only mutated inside lock + raised on UI thread by caller).
        /// </summary>
        public ObservableCollection<ButtonGroup> Groups { get; } = new ObservableCollection<ButtonGroup>();

        /// <summary>
        /// Occurs after the store mutates its state. UI should rebuild / diff in future phases.
        /// </summary>
        public event EventHandler StoreChanged;

        /// <summary>
        /// Occurs with detailed change data (kind + group id) for incremental UI updates.
        /// </summary>
        public event EventHandler<StoreChangedEventArgs> StoreChangedDetailed;

        public ButtonGroup GetGroup(string id)
        {
            lock (_gate)
            {
                return Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Replace (or insert) a dynamic provider supplied group. For Phase 1 we simply replace the existing
        /// group snapshot; UI will rebuild the visual tree. Later we can implement fine grained diff here and emit
        /// structured change events.
        /// </summary>
        public void UpsertProviderGroup(ButtonGroup group)
        {
            if (group == null)
            {
                return;
            }

            bool changed = false;
            lock (_gate)
            {
                var existing = Groups.FirstOrDefault(g => string.Equals(g.Id, group.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    Groups.Add(group);
                    changed = true;
                }
                else if (!ReferenceEquals(existing, group))
                {
                    var index = Groups.IndexOf(existing);
                    if (index >= 0)
                    {
                        Groups[index] = group; // replace snapshot
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                StoreChanged?.Invoke(this, EventArgs.Empty);
                StoreChangedDetailed?.Invoke(this, StoreChangedEventArgs.Upsert(group.Id));
            }
        }

        /// <summary>
        /// Remove a provider group by id.
        /// </summary>
        public void RemoveGroup(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            bool changed = false;
            lock (_gate)
            {
                var existing = Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    Groups.Remove(existing);
                    changed = true;
                }
            }

            if (changed)
            {
                StoreChanged?.Invoke(this, EventArgs.Empty);
                StoreChangedDetailed?.Invoke(this, StoreChangedEventArgs.Removed(id));
            }
        }

        /// <summary>
        /// Bulk replace all groups (used if we generalize later). Not used in Phase 1 but provided for completeness.
        /// </summary>
        public void ReplaceAll(IEnumerable<ButtonGroup> groups)
        {
            if (groups == null)
            {
                return;
            }

            lock (_gate)
            {
                Groups.Clear();
                foreach (var g in groups)
                {
                    if (g != null)
                    {
                        Groups.Add(g);
                    }
                }
            }

            StoreChanged?.Invoke(this, EventArgs.Empty);
            StoreChangedDetailed?.Invoke(this, StoreChangedEventArgs.Reset());
        }
    }
}
