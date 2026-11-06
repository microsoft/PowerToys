// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A pair of displays that has settings but is not next to each other now, such as a display
    /// that is unplugged. The settings wait until the pair is back, until Forget, or until the
    /// module has not seen the pair together for 180 days.
    /// </summary>
    public class CursorBrakeDisconnectedEdgeViewModel
    {
        private readonly Action<CursorBrakeDisconnectedEdgeViewModel> _forget;

        public CursorBrakeDisconnectedEdgeViewModel(string first, string second, string title, string description, Action<CursorBrakeDisconnectedEdgeViewModel> forget)
        {
            First = first;
            Second = second;
            Title = title;
            Description = description;
            _forget = forget;
        }

        /// <summary>Gets the first display's ID.</summary>
        public string First { get; }

        /// <summary>Gets the second display's ID.</summary>
        public string Second { get; }

        public string Title { get; }

        public string Description { get; }

        /// <summary>Removes the pair's settings.</summary>
        public void Forget()
        {
            _forget(this);
        }
    }
}
