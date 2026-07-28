// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ManagedCommon
{
    public static class IdRecoveryHelper
    {
        /// <summary>
        /// Ensures that all items in the provided list have unique IDs. Duplicate IDs are replaced
        /// with the next available unique ID.
        /// </summary>
        /// <param name="items">The list of items that may contain duplicate IDs.</param>
        public static void RecoverInvalidIds<T>(IEnumerable<T> items)
            where T : class, IHasId
        {
            var seenIds = new HashSet<int>();
            int nextAvailableId = 0;

            foreach (var item in items)
            {
                // If this ID is already used, assign a new unique ID.
                if (!seenIds.Add(item.Id))
                {
                    // Find the next unused ID.
                    while (!seenIds.Add(nextAvailableId))
                    {
                        nextAvailableId++;
                    }

                    item.Id = nextAvailableId;
                }
            }
        }
    }

    public interface IHasId
    {
        int Id { get; set; }
    }
}
