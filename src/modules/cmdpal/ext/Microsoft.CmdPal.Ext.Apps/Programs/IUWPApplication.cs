// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>
/// Exposes packaged-application discovery metadata without owning catalog projection.
/// </summary>
public interface IUWPApplication
{
    /// <summary>Gets the localized application name.</summary>
    string Name { get; }

    /// <summary>Gets or sets the localized application description.</summary>
    string Description { get; set; }

    /// <summary>Gets or sets the application user-model ID.</summary>
    string UserModelId { get; set; }

    /// <summary>Gets or sets a value indicating whether discovery considers the application enabled.</summary>
    bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the application may run elevated.</summary>
    bool CanRunElevated { get; set; }

    /// <summary>Gets or sets the resolved list-logo path.</summary>
    string LogoPath { get; set; }

    /// <summary>Gets or sets the resolution state of the list logo.</summary>
    LogoType LogoType { get; set; }

    /// <summary>Gets or sets the resolved jumbo-logo path.</summary>
    string JumboLogoPath { get; set; }

    /// <summary>Gets or sets the resolution state of the jumbo logo.</summary>
    LogoType JumboLogoType { get; set; }

    /// <summary>Gets or sets the package that owns the application.</summary>
    UWP Package { get; set; }

    /// <summary>Resolves theme-appropriate list and jumbo logos.</summary>
    void UpdateLogoPath(Utils.Theme theme);
}
