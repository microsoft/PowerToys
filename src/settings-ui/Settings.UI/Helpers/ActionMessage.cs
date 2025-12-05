// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public sealed class ActionMessage
    {
        [JsonPropertyName("action")]
        public SettingsAction Action { get; set; }

        public static ActionMessage Create(string actionName)
        {
            return new ActionMessage
            {
                Action = new SettingsAction
                {
                    PublishedDate = new SettingsGeneral
                    {
                        ActionName = actionName,
                    },
                },
            };
        }
    }

    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Those are just a define for one simple struct")]
    public sealed class SettingsAction
    {
        [JsonPropertyName("general")]
        public SettingsGeneral PublishedDate { get; set; }
    }

    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Those are just a define for one simple struct")]
    public sealed class SettingsGeneral
        {
        [JsonPropertyName("action_name")]
        public string ActionName { get; set; }
    }
}
