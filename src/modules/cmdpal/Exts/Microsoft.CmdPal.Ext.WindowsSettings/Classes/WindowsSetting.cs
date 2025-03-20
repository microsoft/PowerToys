// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Classes;

/// <summary>
/// A windows setting
/// </summary>
internal sealed class WindowsSetting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsSetting"/> class.
    /// </summary>
    public WindowsSetting()
    {
        Name = string.Empty;
        Command = string.Empty;
        Type = string.Empty;
        ShowAsFirstResult = false;
    }

    /// <summary>
    /// Gets or sets the name of this setting.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the areas of this setting. The order is fixed to the order in json.
    /// </summary>
#pragma warning disable CS8632
    public IList<string>? Areas { get; set; }

    /// <summary>
    /// Gets or sets the command of this setting.
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    /// Gets or sets the type of the windows setting.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the alternative names of this setting.
    /// </summary>
    public IEnumerable<string>? AltNames { get; set; }

    /// <summary>
    /// Gets or sets a additional note of this settings.
    /// <para>(e.g. why is not supported on your system)</para>
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the minimum need Windows build for this setting.
    /// </summary>
    public uint? IntroducedInBuild { get; set; }

    /// <summary>
    /// Gets or sets the Windows build since this settings is not longer present.
    /// </summary>
    public uint? DeprecatedInBuild { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use a higher score as normal for this setting to show it as one of the first results.
    /// </summary>
    public bool ShowAsFirstResult { get; set; }

    /// <summary>
    /// Gets or sets the value with the generated area path as string.
    /// This Property IS NOT PART OF THE DATA IN "WindowsSettings.json".
    /// This property will be filled on runtime by "WindowsSettingsPathHelper".
    /// </summary>
    public string? JoinedAreaPath { get; set; }

    /// <summary>
    /// Gets or sets the value with the generated full settings path (App and areas) as string.
    /// This Property IS NOT PART OF THE DATA IN "WindowsSettings.json".
    /// This property will be filled on runtime by "WindowsSettingsPathHelper".
    /// </summary>
    public string? JoinedFullSettingsPath { get; set; }
#pragma warning restore CS8632
}
