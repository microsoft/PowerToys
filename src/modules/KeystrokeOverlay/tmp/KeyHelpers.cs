// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KeystrokeOverlayUI
{
    public static class KeyHelpers
    {
        public static string GetKeyName(uint vk)
        {
            try
            {
                return ((Windows.System.VirtualKey)vk).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task FadeAndRemoveAsync(
            KeyDisplayItem item,
            ObservableCollection<KeyDisplayItem> list)
        {
            try
            {
                await Task.Delay(900);
                item.Opacity = 0.0;
                await Task.Delay(150);
                list.Remove(item);
            }
            catch
            {
            }
        }
    }
}
