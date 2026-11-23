// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A group of <see cref="CharacterSetPickerItem"/> entries with a header, for use
    /// with a grouped <c>CollectionViewSource</c>.
    /// </summary>
    public partial class CharacterSetPickerGroup : List<CharacterSetPickerItem>
    {
        public CharacterSetPickerGroup(string header, IEnumerable<CharacterSetPickerItem> items)
            : base(items)
        {
            Header = header;
        }

        public string Header { get; }

        // Used by UI Automation as the group's accessible name.
        public override string ToString() => Header;
    }
}
