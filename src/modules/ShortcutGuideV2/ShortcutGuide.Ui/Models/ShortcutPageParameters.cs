// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ShortcutGuide.Models
{
    internal struct ShortcutPageParameters
    {
        public static SeachFilterObservable SearchFilter = new();
    }

    internal sealed class SeachFilterObservable
    {
        public event EventHandler<string>? FilterChanged;

        public void OnFilterChanged(string filter)
        {
            FilterChanged?.Invoke(this, filter);
        }
    }
}
