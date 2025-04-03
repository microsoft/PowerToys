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
    public static class ImageResizerHelper
    {
        /// <summary>
        /// Fixes invalid IDs in the given list by assigning unique values.
        /// It ensures that all IDs are non-zero and unique, correcting any duplicates or empty (zero) IDs.
        /// </summary>
        /// <param name="sizes">The list of size items that may contain invalid IDs.</param>
        public static void RecoverInvalidIds<T>(ObservableCollection<T> sizes)
            where T : IImageSize
        {
            var idSet = new HashSet<int>();
            int newId = 0;

            // Iterate through the list and fix invalid IDs
            foreach (var size in sizes)
            {
                // If the ID is invalid or already exists in the set (duplicate), assign a new unique ID
                if (!idSet.Add(size.Id))
                {
                    // Find the next available unique ID
                    while (idSet.Contains(newId))
                    {
                        newId++;
                    }

                    size.Id = newId;
                    idSet.Add(newId); // Add the newly assigned ID to the set
                }
            }
        }
    }

    public interface IImageSize
    {
        int Id { get; set; }
    }
}
