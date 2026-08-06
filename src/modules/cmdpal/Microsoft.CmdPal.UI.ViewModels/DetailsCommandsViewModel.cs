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

        var newCommands = new List<CommandViewModel>();
        var transferred = false;

        try
        {
            var commands = model.Commands;
            if (commands is not null)
            {
                foreach (var command in commands)
                {
                    var vm = new CommandViewModel(command, PageContext);
                    newCommands.Add(vm);
                    vm.InitializeProperties();
                }
            }

            ReplaceCommands(newCommands);
            transferred = true;

            UpdateProperty(nameof(HasCommands));
            UpdateProperty(nameof(Commands));
        }
        finally
        {
            if (!transferred)
            {
                foreach (var command in newCommands)
                {
                    command.SafeCleanup();
                }
            }
        }
    }

    private void ReplaceCommands(List<CommandViewModel> commands)
    {
        var replacedCommands = Commands;
        Commands = commands;

        foreach (var command in replacedCommands)
        {
            command.SafeCleanup();
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        ReplaceCommands([]);
    }
}
