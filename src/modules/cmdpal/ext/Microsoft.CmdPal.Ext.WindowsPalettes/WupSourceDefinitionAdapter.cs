// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

// CsWinRT runtime classes expose default-interface members directly, but cannot be cast to
// their exclusive interfaces. Forward through the runtime class instead of using As<T>().
internal sealed partial class WupSourceDefinitionAdapter(PaletteExtensionDefinition definition) : IPaletteExtensionDefinition
{
    public string ExtensionId => definition.ExtensionId;

    public string PackageFamilyName => definition.PackageFamilyName;

    public string ApplicationId => definition.ApplicationId;

    public string DisplayName => definition.DisplayName;

    public string Description => definition.Description;

    public PaletteIconData Icon => definition.Icon;

    public PaletteItemFactory ItemFactory => definition.ItemFactory;

    public string[] AcceptedKindSpecs => definition.AcceptedKindSpecs;

    public string Publisher => definition.Publisher;

    public IDictionary<string, object> Properties => definition.Properties;

    public PaletteKinds Kinds => definition.Kinds;

    public PaletteKinds ContainedKinds => definition.ContainedKinds;

    public IPaletteItemSourceExtension CreateExtension(PaletteExtensionHost host) => definition.CreateExtension(host);
}
