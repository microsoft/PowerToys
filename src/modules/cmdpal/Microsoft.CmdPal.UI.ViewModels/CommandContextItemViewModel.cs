// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandContextItemViewModel : CommandItemViewModel
{
    private readonly ExtensionObject<ICommandContextItem> _contextItemModel;

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public CommandContextItemViewModel(ICommandContextItem contextItem)
        : base(new(contextItem))
    {
        _contextItemModel = new(contextItem);
    }

    protected override void Initialize()
    {
        base.Initialize();
        var contextItem = _contextItemModel.Unsafe;
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
