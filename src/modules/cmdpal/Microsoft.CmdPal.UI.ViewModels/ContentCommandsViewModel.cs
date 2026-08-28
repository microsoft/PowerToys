// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentCommandsViewModel(ICommandsContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<ICommandsContent>(model, context)
{
    private readonly Lock _gate = new();
    private bool _stopped;

    public IReadOnlyList<CommandViewModel> Commands { get; private set; } = [];

    protected override void ReadProperties()
    {
        var next = new List<CommandViewModel>();
        try
        {
            foreach (var command in Model.Commands ?? [])
            {
                var viewModel = new CommandViewModel(command, PageContext);
                next.Add(viewModel);
                viewModel.InitializeProperties();
            }

            IReadOnlyList<CommandViewModel> removed;
            lock (_gate)
            {
                if (_stopped)
                {
                    return;
                }

                removed = Commands;
                Commands = next;
            }

            next = [];
            foreach (var command in removed)
            {
                command.SafeCleanup();
            }

            UpdateProperty(nameof(Commands));
        }
        finally
        {
            foreach (var command in next)
            {
                command.SafeCleanup();
            }
        }
    }

    protected override void UnsafeCleanup()
    {
        IReadOnlyList<CommandViewModel> removed;
        lock (_gate)
        {
            _stopped = true;
            removed = Commands;
            Commands = [];
        }

        foreach (var command in removed)
        {
            command.SafeCleanup();
        }

        base.UnsafeCleanup();
    }
}
