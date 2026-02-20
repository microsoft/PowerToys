// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.PowerToys.Settings.UI.Helpers;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    /// <summary>
    /// View model for a group of releases (grouped by major.minor version).
    /// </summary>
    public class ScoobeReleaseGroupViewModel
    {
        /// <summary>
        /// Gets the list of releases in this group.
        /// </summary>
        public IList<PowerToysReleaseInfo> Releases { get; }

        /// <summary>
        /// Gets the version text to display (e.g., "0.96.0").
        /// </summary>
        public string VersionText { get; }

        /// <summary>
        /// Gets the date text to display (e.g., "December 2025").
        /// </summary>
        public string DateText { get; }

        public ScoobeReleaseGroupViewModel(IList<PowerToysReleaseInfo> releases)
        {
            Releases = releases ?? throw new ArgumentNullException(nameof(releases));

            if (releases.Count > 0)
            {
                var latestRelease = releases[0];
                VersionText = GetVersionFromRelease(latestRelease);
                DateText = latestRelease.PublishedDate.ToString("Y", CultureInfo.CurrentCulture);
            }
            else
            {
                VersionText = "Unknown";
                DateText = string.Empty;
            }
        }

        internal static string GetVersionFromRelease(PowerToysReleaseInfo release)
        {
            // TagName is typically like "v0.96.0", Name might be "Release v0.96.0"
            string version = release.TagName ?? release.Name ?? "Unknown";
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                version = version.Substring(1);
            }

            return version;
        }
    }
}
