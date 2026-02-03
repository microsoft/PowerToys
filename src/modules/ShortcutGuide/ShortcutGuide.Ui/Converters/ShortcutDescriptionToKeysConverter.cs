// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Documents;
using ManagedCommon;
using Microsoft.UI.Xaml.Data;
using ShortcutGuide.Models;
using Windows.System;

namespace ShortcutGuide.Converters
{
    public sealed partial class ShortcutDescriptionToKeysConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ShortcutDescription description)
            {
                // Populate keysList with the keys from the ShortcutDescription
                return this.GetKeysList(description);
            }
            else
            {
                List<object> keysList = [string.Empty];
                return keysList;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }

        public List<object> GetKeysList(ShortcutDescription description)
        {
            List<object> shortcutList = [];

            if (description.Win)
            {
                shortcutList.Add(92); // The Windows key or button.
            }

            if (description.Ctrl)
            {
                shortcutList.Add("Ctrl");
            }

            if (description.Alt)
            {
                shortcutList.Add("Alt");
            }

            if (description.Shift)
            {
                shortcutList.Add(16); // The Shift key or button.
            }

            foreach (var key in description.Keys)
            {
                // Try to parse a string key number to a VirtualKey
                if (int.TryParse(key, out int keyCode))
                {
                    shortcutList.Add(keyCode);
                }
                else
                {
                    switch (key)
                    {
                        // https://learn.microsoft.com/uwp/api/windows.system.virtualkey?view=winrt-20348
                        case "Up":
                            shortcutList.Add(38); // The Up Arrow key or button.
                            break;
                        case "Down":
                            shortcutList.Add(40); // The Down Arrow key or button.
                            break;
                        case "Left":
                            shortcutList.Add(37); // The Left Arrow key or button.
                            break;
                        case "Right":
                            shortcutList.Add(39); // The Right Arrow key or button.
                            break;
                        case "Back":
                            shortcutList.Add(8); // The Back key or button.
                            break;
                        case "<TASKBAR1-9>":
                            shortcutList.Add("Num");
                            break;
                        default:
                            shortcutList.Add(key); // Add other keys as strings.
                            break;
                    }
                }
            }

            return shortcutList;
        }
    }
}
