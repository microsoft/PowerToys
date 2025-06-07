// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ShortcutGuide.Models
{
    internal struct ShortcutPageParameters
    {
        public static SeachFilterObservable SearchFilter = new();

        public static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts = [];

        public static string CurrentPageName = string.Empty;

        public static FrameHeightObservable FrameHeight = new();

        internal sealed class SeachFilterObservable
        {
            public event EventHandler<string>? FilterChanged;

            public void OnFilterChanged(string filter)
            {
                FilterChanged?.Invoke(this, filter);
            }
        }

        internal sealed class FrameHeightObservable
        {
            public event EventHandler<double>? FrameHeightChanged;

            public void OnFrameHeightChanged(double height)
            {
                if (height <= 0)
                {
                    return;
                }

                FrameHeightChanged?.Invoke(this, height);
            }
        }
    }
}
