// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// The content of <c>detected-edges.json</c>, which the CursorBrake module writes into its
    /// settings folder: the monitors it sees, the edges between them and when each pair of
    /// monitors was last seen next to each other. Settings only reads it.
    /// </summary>
    public class CursorBrakeDetectedEdges
    {
        /// <summary>The file name in the module's settings folder.</summary>
        public const string FileName = "detected-edges.json";

        /// <summary>The file version this model reads.</summary>
        public const int CurrentVersion = 1;

        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every monitor has an ID. Edges of a monitor
        /// without one cannot carry settings.
        /// </summary>
        [JsonPropertyName("identified")]
        public bool Identified { get; set; }

        [JsonPropertyName("monitors")]
        public List<CursorBrakeDetectedMonitor> Monitors { get; init; } = new List<CursorBrakeDetectedMonitor>();

        [JsonPropertyName("edges")]
        public List<CursorBrakeDetectedEdge> Edges { get; init; } = new List<CursorBrakeDetectedEdge>();

        [JsonPropertyName("seen")]
        public List<CursorBrakeSeenPair> Seen { get; init; } = new List<CursorBrakeSeenPair>();
    }
}
