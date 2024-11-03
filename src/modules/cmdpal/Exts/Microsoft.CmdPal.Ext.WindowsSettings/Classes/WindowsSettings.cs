// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Classes;

/// <summary>
/// A class that contain all possible windows settings
/// </summary>
internal sealed class WindowsSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsSettings"/> class with an empty settings list.
    /// </summary>
    public WindowsSettings()
    {
        Settings = Enumerable.Empty<WindowsSetting>();
    }

    /// <summary>
    /// Gets or sets a list with all possible windows settings
    /// </summary>
    public IEnumerable<WindowsSetting> Settings { get; set; }
}
