// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace FancyZonesEditor.Models
{
    public class FastAccessKeysModel
    {
        public static List<int> FreeKeys { get; } = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static SortedDictionary<int, string> SelectedKeys { get; } = new SortedDictionary<int, string>();

        public FastAccessKeysModel()
        {
        }

        public static void FreeKey(int key)
        {
            SelectedKeys.Remove(key);
            FreeKeys.Add(key);
        }

        public static bool SelectKey(int key, string uuid)
        {
            if (SelectedKeys.ContainsKey(key))
            {
                return false;
            }

            FreeKeys.Remove(key);
            SelectedKeys.Add(key, uuid);
            return true;
        }
    }
}
