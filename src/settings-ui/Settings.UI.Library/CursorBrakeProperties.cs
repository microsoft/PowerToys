// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorBrakeProperties
    {
        /// <summary>The push distance a settings file without one gets, in logical pixels.</summary>
        public const int DefaultPushDistance = 300;

        /// <summary>The range the module clamps a push distance into, in logical pixels.</summary>
        public const int MinPushDistance = 0;

        /// <inheritdoc cref="MinPushDistance"/>
        public const int MaxPushDistance = 1000;

        [JsonPropertyName("push_distance")]
        public IntProperty PushDistance { get; set; }

        [JsonPropertyName("free_return")]
        public BoolProperty FreeReturn { get; set; }

        [JsonPropertyName("edge_filter")]
        public IntProperty EdgeFilter { get; set; }

        [JsonPropertyName("disable_while_dragging")]
        public BoolProperty DisableWhileDragging { get; set; }

        [JsonPropertyName("cross_modifier")]
        public IntProperty CrossModifier { get; set; }

        public CursorBrakeProperties()
        {
            PushDistance = new IntProperty(DefaultPushDistance);
            FreeReturn = new BoolProperty(true);
            EdgeFilter = new IntProperty((int)CursorBrakeEdgeFilter.All);
            DisableWhileDragging = new BoolProperty(false);
            CrossModifier = new IntProperty((int)CursorBrakeCrossModifier.Ctrl);
        }
    }
}
