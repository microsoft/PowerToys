// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandContextItemViewModel(ICommandContextItem contextItem, WeakReference<IPageContext> context) : CommandItemViewModel(new(contextItem), context)
{
    public new ExtensionObject<ICommandContextItem> Model { get; } = new(contextItem);

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        base.InitializeProperties();

        var contextItem = Model.Unsafe;
        if (contextItem == null)
        {
            return; // throw?
        }

        IsCritical = contextItem.IsCritical;
        if (contextItem.RequestedShortcut != null)
        {
            RequestedShortcut = new(
                contextItem.RequestedShortcut.Modifiers,
                contextItem.RequestedShortcut.Vkey,
                contextItem.RequestedShortcut.ScanCode);
        }
    }
}
