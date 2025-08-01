// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ShortcutGuide.Models
{
    /// <summary>
    /// Represents the parameters for the shortcut page in the Shortcut Guide module.
    /// </summary>
    internal struct ShortcutPageParameters
    {
        /// <summary>
        /// Gets or sets the content of the search box.
        /// </summary>
        public static SearchFilterObservable SearchFilter = new();

        /// <summary>
        /// Gets or sets the pinned shortcuts for the Shortcut Guide.
        /// </summary>
        public static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts = [];

        /// <summary>
        /// Gets or sets the name of the current page being displayed in the Shortcut Guide.
        /// </summary>
        public static string CurrentPageName = string.Empty;

        /// <summary>
        /// The height of the frame that displays the shortcuts.
        /// </summary>
        public static FrameHeightObservable FrameHeight = new();

        internal sealed class SearchFilterObservable
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
