// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

// ... (Other necessary using directives)

namespace ColorPicker.Helpers
{
    public class ColorEntry
    {
        public string ColorCode { get; set; }
        public bool IsPinned { get; set; }
    }

    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        // History management with pinning support
        private static readonly List<ColorEntry> _colorHistory = new List<ColorEntry>();

        public static void AddToHistory(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return;

            var existing = _colorHistory.FirstOrDefault(c => c.ColorCode == color);
            if (existing != null)
            {
                _colorHistory.Remove(existing);
                _colorHistory.Insert(0, existing);
                return;
            }

            _colorHistory.Insert(0, new ColorEntry { ColorCode = color, IsPinned = false });

            if (_colorHistory.Count > 10)
            {
                var lastNonPinned = _colorHistory.LastOrDefault(c => !c.IsPinned);
                if (lastNonPinned != null) _colorHistory.Remove(lastNonPinned);
            }
        }

        public static void TogglePin(string color)
        {
            var entry = _colorHistory.FirstOrDefault(c => c.ColorCode == color);
            if (entry != null) entry.IsPinned = !entry.IsPinned;
        }

        public static List<ColorEntry> GetHistory() => _colorHistory.ToList();

        // ... (Rest of the class implementation)
    }
}
