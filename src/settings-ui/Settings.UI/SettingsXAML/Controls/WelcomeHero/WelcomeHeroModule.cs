// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// A module shown in the Welcome hero.
    /// </summary>
    /// <param name="AssetName">File name (without extension) of the icon in <see cref="WelcomeHeroModules.AssetFolder"/>.</param>
    /// <param name="NavigationTag">Tag of the matching OOBE navigation item (a <c>PowerToysModules</c> name).</param>
    /// <param name="NameResourceKey">Resource key of the localized module name.</param>
    public sealed record WelcomeHeroModule(string AssetName, string NavigationTag, string NameResourceKey);
}
