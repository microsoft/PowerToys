// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace FancyZonesEditor.Models
{
    public class FastAccessKeysModel
    {
        public static SortedDictionary<int, string> SelectedKeys { get; } = new SortedDictionary<int, string>()
        {
            { 0, string.Empty },
            { 1, string.Empty },
            { 2, string.Empty },
            { 3, string.Empty },
            { 4, string.Empty },
            { 5, string.Empty },
            { 6, string.Empty },
            { 7, string.Empty },
            { 8, string.Empty },
            { 9, string.Empty },
        };

        public FastAccessKeysModel()
        {
        }

        public static void FreeKey(int key)
        {
            if (SelectedKeys.ContainsKey(key))
            {
                SelectedKeys[key] = string.Empty;
            }
        }

        public static bool SelectKey(int key, string uuid)
        {
            if (!SelectedKeys.ContainsKey(key))
            {
                return false;
            }

            SelectedKeys[key] = uuid;
            return true;
        }
    }
}
