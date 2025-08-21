// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.XamlIndexBuilder
{
    public class SearchableElementMetadata
    {
        public string PageName { get; set; }

        public EntryType Type { get; set; }

        public string ParentElementName { get; set; }

        public string ElementName { get; set; }

        public string ElementUid { get; set; }

        public string Icon { get; set; }
    }
}
