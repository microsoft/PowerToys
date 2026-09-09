// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

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

        List<CommandViewModel> commands = [];
        try
        {
            foreach (var command in model.Commands ?? [])
            {
                var vm = new CommandViewModel(command, PageContext);
                commands.Add(vm);
                vm.InitializeProperties();
            }
        }
        catch
        {
            commands.ForEach(vm => vm.SafeCleanup());
            throw;
        }

        var previous = Commands;
        Commands = commands;
        previous.ForEach(vm => vm.SafeCleanup());
        UpdateProperty(nameof(HasCommands));
        UpdateProperty(nameof(Commands));
    }

    protected override void UnsafeCleanup()
    {
        var previous = Commands;
        Commands = [];
        previous.ForEach(vm => vm.SafeCleanup());
        base.UnsafeCleanup();
    }
}
