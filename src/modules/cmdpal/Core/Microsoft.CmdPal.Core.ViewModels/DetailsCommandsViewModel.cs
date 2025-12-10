// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class DetailsCommandsViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    public List<CommandViewModel> Commands { get; private set; } = [];

    public bool HasCommands => Commands.Count > 0;

    private readonly ExtensionObject<IDetailsCommands> _dataModel =
        new(_detailsElement.Data as IDetailsCommands);

    public override void InitializeProperties()
    {
        base.InitializeProperties();
        var model = _dataModel.Unsafe;
        if (model is null)
        {
            return;
        }

        Commands = model
            .Commands?
            .Select(c =>
            {
                var vm = new CommandViewModel(c, PageContext);
                vm.InitializeProperties();
                return vm;
            })
            .ToList() ?? [];
        UpdateProperty(nameof(HasCommands));
        UpdateProperty(nameof(Commands));
    }
}
