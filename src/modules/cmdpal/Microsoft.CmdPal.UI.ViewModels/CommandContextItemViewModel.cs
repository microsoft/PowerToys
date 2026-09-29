// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class CommandContextItemViewModel : CommandItemViewModel, IContextItemViewModel
{
    internal static readonly KeyChord PrimaryShortcut = new(0, (int)VirtualKey.Enter, 0);
    internal static readonly KeyChord SecondaryShortcut = new(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0);

    private readonly KeyChord nullKeyChord = new(0, 0, 0);

    private DefaultShortcutRole _defaultShortcutRole;

    public new ExtensionObject<ICommandContextItem> Model { get; }

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public bool HasRequestedShortcut => RequestedShortcut is not null && (RequestedShortcut.Value != nullKeyChord);

    public KeyChord? DisplayShortcut => HasRequestedShortcut ? RequestedShortcut : DefaultShortcut;

    private KeyChord? DefaultShortcut => ShortcutForRole(_defaultShortcutRole);

    internal void SetDefaultShortcut(DefaultShortcutRole role)
    {
        if (_defaultShortcutRole != role)
        {
            _defaultShortcutRole = role;
            UpdateProperty(nameof(DisplayShortcut));
        }
    }

    private static KeyChord? ShortcutForRole(DefaultShortcutRole role) => role switch
    {
        DefaultShortcutRole.Primary => PrimaryShortcut,
        DefaultShortcutRole.Secondary => SecondaryShortcut,
        _ => null,
    };

    public CommandContextItemViewModel(ICommandContextItem contextItem, WeakReference<IPageContext> context)
        : base(new(contextItem), context, contextMenuFactory: null)
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

        var requestedShortcut = contextItem.RequestedShortcut;

        // Enter and Ctrl+Enter belong to the host; neither lookup nor hints may expose extension requests for them.
        RequestedShortcut = requestedShortcut.Vkey == (int)VirtualKey.Enter &&
            requestedShortcut.Modifiers is VirtualKeyModifiers.None or VirtualKeyModifiers.Control ? null : requestedShortcut;
    }
}

internal enum DefaultShortcutRole
{
    None,
    Primary,
    Secondary,
}
