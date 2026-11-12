// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Minimal contract for an entry that participates in PowerDisplay's per-monitor
    /// retention/preservation logic (see <c>MonitorSettingsRebuilder</c>).
    /// </summary>
    /// <remarks>
    /// Defined here in <c>PowerDisplay.Models</c> so the rebuilder in
    /// <c>PowerDisplay.Lib</c> can operate on it without having to reference the
    /// heavier <c>Settings.UI.Library</c> assembly.
    /// </remarks>
    public interface IRetainableMonitor
    {
        /// <summary>Gets the stable monitor identifier (DevicePath-derived).</summary>
        string Id { get; }

        /// <summary>Gets a value indicating whether the user has hidden this monitor; hidden entries bypass the retention age check.</summary>
        bool IsHidden { get; }

        /// <summary>Gets or sets the UTC timestamp of the last successful discovery; null on entries from old PowerDisplay versions.</summary>
        DateTime? LastSeenUtc { get; set; }
    }
}
