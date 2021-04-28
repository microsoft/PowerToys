// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Classes
{
    internal interface IWindowsSetting
    {
        /// <summary>
        /// Gets or sets the area of this setting.
        /// </summary>
        string Area { get; set; }

        /// <summary>
        /// Gets or sets the name of this setting.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the command of this setting
        /// </summary>
        string Command { get; set; }

        /// <summary>
        /// Gets or sets the alternative names of this setting.
        /// </summary>
        IEnumerable<string>? AltNames { get; set; }

        /// <summary>
        /// Gets or sets a additional note of settings is not longer present.
        /// </summary>
        string? Note { get; set; }

        /// <summary>
        /// Gets or sets the minimum need Windows version for this setting.
        /// </summary>
        ushort? IntroducedInVersion { get; set; }

        /// <summary>
        /// Gets or sets the Windows version since this settings is not longer present.
        /// </summary>
        ushort? DeprecatedInVersion { get; set; }
    }
}
