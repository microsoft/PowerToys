// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCommon
{
    public static class IdRecoveryHelper
    {
        /// <summary>
        /// Fixes invalid IDs in the given list by assigning unique values.
        /// It ensures that all IDs are non-empty and unique, correcting any duplicates or empty IDs.
        /// </summary>
        /// <param name="items">The list of items that may contain invalid IDs.</param>
        public static void RecoverInvalidIds<T>(IEnumerable<T> items)
            where T : class, IHasId
        {
            var idSet = new HashSet<int>();
            int newId = 0;
            var sortedItems = items.OrderBy(i => i.Id).ToList(); // Sort items by ID for consistent processing

            // Iterate through the list and fix invalid IDs
            foreach (var item in sortedItems)
            {
                // If the ID is invalid or already exists in the set (duplicate), assign a new unique ID
                if (!idSet.Add(item.Id))
                {
                    // Find the next available unique ID
                    while (idSet.Contains(newId))
                    {
                        newId++;
                    }

                    item.Id = newId;
                    idSet.Add(newId); // Add the newly assigned ID to the set
                }
            }
        }
    }

    public interface IHasId
    {
        int Id { get; set; }
    }
}
