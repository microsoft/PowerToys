// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed partial class SuggestionItem
    {
        public string Header { get; init; }

        public string Icon { get; init; }

        public string PageTypeName { get; init; }

        public string ElementName { get; init; }

        public string ParentElementName { get; init; }

        public string Subtitle { get; init; }

        public bool IsShowAll { get; init; }

        public bool IsNoResults { get; init; }
    }
}
