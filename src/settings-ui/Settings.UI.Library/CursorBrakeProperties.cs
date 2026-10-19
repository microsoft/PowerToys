// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorBrakeProperties
    {
        /// <summary>The push distance edges at Default use, in logical pixels.</summary>
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

        [JsonPropertyName("fullscreen_behavior")]
        public IntProperty FullscreenBehavior { get; set; }

        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }

        // Per-edge values. An edge without an entry uses the settings above. A list has no
        // command-line form, so the command line leaves it out, like Keyboard Manager's lists. The
        // converter reads a malformed entry alone as null, as the module skips it alone.
        [CmdConfigureIgnore]
        [JsonPropertyName("edges")]
        [JsonConverter(typeof(CursorBrakeEdgesConverter))]
        public GenericProperty<List<CursorBrakeEdgeSetting>> Edges { get; set; }

        public CursorBrakeProperties()
        {
            PushDistance = new IntProperty(DefaultPushDistance);
            FreeReturn = new BoolProperty(true);
            EdgeFilter = new IntProperty((int)CursorBrakeEdgeFilter.All);
            DisableWhileDragging = new BoolProperty(false);
            CrossModifier = new IntProperty((int)CursorBrakeCrossModifier.Ctrl);
            FullscreenBehavior = new IntProperty((int)CursorBrakeFullscreenBehavior.Same);
            ExcludedApps = new StringProperty(); // One app per line
            Edges = new GenericProperty<List<CursorBrakeEdgeSetting>>(new List<CursorBrakeEdgeSetting>());
        }
    }
}
