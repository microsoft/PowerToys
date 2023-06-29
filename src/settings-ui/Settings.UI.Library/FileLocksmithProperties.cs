// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FileLocksmithProperties
    {
        public FileLocksmithProperties()
        {
            ExtendedContextMenuOnly = new BoolProperty(false);
        }

        [JsonPropertyName("bool_show_extended_menu")]
        public BoolProperty ExtendedContextMenuOnly { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}
