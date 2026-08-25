// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsCommandsViewModel : DetailsElementViewModel
{
    public List<CommandViewModel> Commands { get; private set; } = [];

    public bool HasCommands => Commands.Count > 0;

    private readonly ExtensionObject<IDetailsCommands> _dataModel;

    public DetailsCommandsViewModel(IDetailsElement detailsElement, WeakReference<IPageContext> context)
        : this(detailsElement, context, null)
    {
    }

    internal DetailsCommandsViewModel(
        IDetailsElement detailsElement,
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext)
        : base(detailsElement, context, fallbackContext)
    {
        _dataModel = new(detailsElement.Data as IDetailsCommands);
    }

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
                var vm = new CommandViewModel(c, PageContext, FallbackContext);
                vm.InitializeProperties();
                return vm;
            })
            .ToList() ?? [];
        UpdateProperty(nameof(HasCommands));
        UpdateProperty(nameof(Commands));
    }
}
