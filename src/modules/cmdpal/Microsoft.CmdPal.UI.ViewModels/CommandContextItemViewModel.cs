// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandContextItemViewModel(ICommandContextItem contextItem, TaskScheduler scheduler) : CommandItemViewModel(new(contextItem), scheduler)
{
    private readonly ExtensionObject<ICommandContextItem> _contextItemModel = new(contextItem);

    public bool IsCritical { get; private set; }

    public KeyChord? RequestedShortcut { get; private set; }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

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
