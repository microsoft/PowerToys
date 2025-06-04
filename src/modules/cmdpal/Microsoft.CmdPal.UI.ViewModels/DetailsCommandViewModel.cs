// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsCommandViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    private readonly ExtensionObject<IDetailsCommand> _dataModel =
        new(_detailsElement.Data as IDetailsCommand);

    public string Text { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public ICommand? Command { get; private set; }

    public override void InitializeProperties()
    {
        base.InitializeProperties();
        var model = _dataModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Text = model.Command.Name ?? string.Empty;
        Icon = new(model.Command.Icon);
        Command = model.Command;

        UpdateProperty(nameof(Text));
        UpdateProperty(nameof(Icon));
        UpdateProperty(nameof(Command));
    }
}
