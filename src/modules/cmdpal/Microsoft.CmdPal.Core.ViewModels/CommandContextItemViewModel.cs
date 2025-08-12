// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class CommandContextItemViewModel(ICommandContextItem contextItem, WeakReference<IPageContext> context) : CommandItemViewModel(new(contextItem), context), IContextItemViewModel
{
    private readonly KeyChord nullKeyChord = new(0, 0, 0);

    public new ExtensionObject<ICommandContextItem> Model { get; } = new(contextItem);

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public bool HasRequestedShortcut => RequestedShortcut != null && (RequestedShortcut.Value != nullKeyChord);

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

        // I actually don't think this will ever actually be null, because
        // KeyChord is a struct, which isn't nullable in WinRT
        if (contextItem.RequestedShortcut != null)
        {
            RequestedShortcut = new(
                contextItem.RequestedShortcut.Modifiers,
                contextItem.RequestedShortcut.Vkey,
                contextItem.RequestedShortcut.ScanCode);
        }
    }
}
