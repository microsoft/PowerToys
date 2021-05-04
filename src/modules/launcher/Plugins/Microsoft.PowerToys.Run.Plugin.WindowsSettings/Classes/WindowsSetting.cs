// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Enumerations;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Classes
{
    /// <summary>
    /// A windows setting
    /// </summary>
    internal class WindowsSetting : IWindowsSetting
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSetting"/> class.
        /// </summary>
        public WindowsSetting()
        {
            Name = string.Empty;
            Area = string.Empty;
            Command = string.Empty;
            Type = WindowsSettingsType.SettingsApp;
        }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public string Area { get; set; }

        /// <inheritdoc/>
        public string Command { get; set; }

        /// <inheritdoc/>
        public WindowsSettingsType Type { get; set; }

        /// <inheritdoc/>
        public IEnumerable<string>? AltNames { get; set; }

        /// <inheritdoc/>
        public string? Note { get; set; }

        /// <inheritdoc/>
        public ushort? IntroducedInVersion { get; set; }

        /// <inheritdoc/>
        public ushort? DeprecatedInVersion { get; set; }
    }
}
