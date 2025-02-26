// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class NewPlusProperties
    {
        public const string ModuleName = "NewPlus";

        public NewPlusProperties()
        {
            HideFileExtension = new BoolProperty(true);
            HideStartingDigits = new BoolProperty(true);
            TemplateLocation = new StringProperty(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "NewPlus", "Templates"));
            ReplaceVariables = new BoolProperty(false);
        }

        [JsonPropertyName("HideFileExtension")]
        public BoolProperty HideFileExtension { get; set; }

        [JsonPropertyName("HideStartingDigits")]
        public BoolProperty HideStartingDigits { get; set; }

        [JsonPropertyName("TemplateLocation")]
        public StringProperty TemplateLocation { get; set; }

        [JsonPropertyName("ReplaceVariables")]
        public BoolProperty ReplaceVariables { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }
}
