// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// A profile reference that retains an unresolved legacy numeric id until its UUID is known.
    /// </summary>
    [JsonConverter(typeof(ProfileIdPropertyJsonConverter))]
    public record ProfileIdProperty : ICmdLineRepresentable
    {
        public ProfileIdProperty()
        {
        }

        public ProfileIdProperty(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; set; }

        public int? LegacyId { get; set; }

        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = null;
            if (Guid.TryParse(cmd, out var id))
            {
                result = new ProfileIdProperty(id);
                return true;
            }

            // Settings get/set must round-trip a legacy reference even before profiles.json
            // has supplied its UUID. This does not change the apply-profile CLI's UUID input.
            if (int.TryParse(cmd, NumberStyles.None, CultureInfo.InvariantCulture, out var legacyId) && legacyId >= 0)
            {
                result = new ProfileIdProperty { LegacyId = legacyId > 0 ? legacyId : null };
                return true;
            }

            return false;
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = Value == Guid.Empty && LegacyId is > 0
                ? LegacyId.Value.ToString(CultureInfo.InvariantCulture)
                : Value.ToString("D");
            return true;
        }
    }
}
