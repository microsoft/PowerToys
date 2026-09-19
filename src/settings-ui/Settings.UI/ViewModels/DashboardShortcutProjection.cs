// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal static class DashboardShortcutProjection
    {
        internal static void Refresh(IEnumerable<DashboardListItem> modules, ObservableCollection<DashboardListItem> shortcuts)
        {
            // The exposed collection owns the rows; removed modules are not kept in a separate cache.
            var existingRows = shortcuts.ToDictionary(row => row.Tag);
            var seenModules = new HashSet<ModuleType>();
            var desiredRows = new List<DashboardListItem>();

            foreach (var module in modules)
            {
                if (!seenModules.Add(module.Tag))
                {
                    throw new ArgumentException("Dashboard modules must have unique module types.", nameof(modules));
                }

                if (!module.IsEnabled)
                {
                    continue;
                }

                var items = module.DashboardModuleItems
                    .Where(item => item is DashboardModuleShortcutItem or DashboardModuleActivationItem)
                    .ToList();
                if (items.Count == 0)
                {
                    continue;
                }

                if (!existingRows.TryGetValue(module.Tag, out var row))
                {
                    row = new DashboardListItem { Tag = module.Tag };
                }

                row.Icon = module.Icon;
                row.IsLocked = module.IsLocked;
                row.Label = module.Label;
                row.UpdateStatus(module.IsEnabled);
                row.EnabledChangedCallback = module.EnabledChangedCallback;
                Reconcile(row.DashboardModuleItems, items);
                desiredRows.Add(row);
            }

            Reconcile(shortcuts, desiredRows);
        }

        private static void Reconcile<T>(ObservableCollection<T> target, List<T> desired)
        {
            // Remove first so disabling one module does not move all the rows that followed it.
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(target[i]))
                {
                    target.RemoveAt(i);
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                var currentIndex = target.IndexOf(desired[i]);
                if (currentIndex < 0)
                {
                    target.Insert(i, desired[i]);
                }
                else if (currentIndex != i)
                {
                    target.Move(currentIndex, i);
                }
            }
        }
    }
}
