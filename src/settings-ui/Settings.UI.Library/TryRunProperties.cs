// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class TryRunProperties
    {
        [JsonPropertyName("show_in_context_menu")]
        public BoolProperty ShowInContextMenu { get; set; } = new BoolProperty(true);
    }
}
