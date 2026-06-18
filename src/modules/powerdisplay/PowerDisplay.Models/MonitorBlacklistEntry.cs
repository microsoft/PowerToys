// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// One entry in a PowerDisplay monitor blacklist. Used both for the built-in
    /// list shipped with PowerToys (loaded via <see cref="BuiltInMonitorBlacklist"/>)
    /// and for the user-editable custom list persisted on <c>PowerDisplayProperties</c>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="EdidId"/> is the 7–8 character PnP hardware identifier extracted
    /// from a <c>Monitor.Id</c> by <c>MonitorIdentity.EdidIdFromMonitorId</c> (e.g.
    /// <c>"DELD1A8"</c>, <c>"BOE0900"</c>). It is normalized to uppercase and trimmed
    /// on write; matching is case-insensitive as a defense-in-depth measure.</para>
    /// <para><see cref="Comments"/> is free text rendered as-is. The built-in JSON ships
    /// English-only comments; user input is not localized.</para>
    /// </remarks>
    public class MonitorBlacklistEntry
    {
        [JsonPropertyName("edidId")]
        public string EdidId { get; set; } = string.Empty;

        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;
    }
}
