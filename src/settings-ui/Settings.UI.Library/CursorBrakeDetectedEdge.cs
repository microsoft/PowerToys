// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorBrakeDetectedEdge
    {
        /// <summary>The <see cref="Axis"/> of an edge between monitors side by side.</summary>
        public const string SideBySideAxis = "vertical";

        /// <summary>Gets the indices of the two monitors in <see cref="CursorBrakeDetectedEdges.Monitors"/>.</summary>
        [JsonPropertyName("monitors")]
        public List<int> Monitors { get; init; } = new List<int>();

        /// <summary>
        /// Gets or sets "vertical" for monitors side by side, sharing the line x = <see cref="At"/>,
        /// or "horizontal" for stacked ones sharing y = <see cref="At"/>.
        /// </summary>
        [JsonPropertyName("axis")]
        public string Axis { get; set; } = string.Empty;

        /// <summary>Gets a value indicating whether the two monitors are side by side.</summary>
        [JsonIgnore]
        public bool IsSideBySide => Axis == SideBySideAxis;

        [JsonPropertyName("at")]
        public int At { get; set; }

        /// <summary>Gets or sets where the shared span starts along the line.</summary>
        [JsonPropertyName("from")]
        public int From { get; set; }

        /// <summary>Gets or sets where the shared span ends along the line, outside it.</summary>
        [JsonPropertyName("to")]
        public int To { get; set; }
    }
}
