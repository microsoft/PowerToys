// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerAccentLanguageGroupModel : List<PowerAccentLanguageModel>
    {
        public PowerAccentLanguageGroupModel(List<PowerAccentLanguageModel> languages, string group)
            : base(languages)
        {
            this.Group = group;
        }

        public string Group { get; init; }
    }
}
