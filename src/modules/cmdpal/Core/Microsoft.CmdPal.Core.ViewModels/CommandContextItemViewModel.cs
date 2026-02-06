// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class CommandContextItemViewModel : CommandItemViewModel, IContextItemViewModel
{
    private readonly KeyChord nullKeyChord = new(0, 0, 0);

    public new ExtensionObject<ICommandContextItem> Model { get; }

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public bool HasRequestedShortcut => RequestedShortcut is not null && (RequestedShortcut.Value != nullKeyChord);

    public CommandContextItemViewModel(ICommandContextItem contextItem, WeakReference<IPageContext> context)
        : base(new(contextItem), context)
    {
        Model = new(contextItem);
        IsContextMenuItem = true;
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        base.InitializeProperties();

        var contextItem = Model.Unsafe;
        if (contextItem is null)
        {
            return; // throw?
        }

        IsCritical = contextItem.IsCritical;

        RequestedShortcut = new(
            contextItem.RequestedShortcut.Modifiers,
            contextItem.RequestedShortcut.Vkey,
            contextItem.RequestedShortcut.ScanCode);
    }
}
