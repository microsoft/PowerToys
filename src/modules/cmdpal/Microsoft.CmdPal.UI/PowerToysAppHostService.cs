// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

internal sealed class PowerToysAppHostService : IAppHostService
{
    public AppExtensionHost GetDefaultHost()
    {
        return CommandPaletteHost.Instance;
    }

    public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost)
    {
        AppExtensionHost? topLevelHost = null;
        if (context is TopLevelViewModel topLevelViewModel)
        {
            topLevelHost = topLevelViewModel.ExtensionHost;
        }

        return topLevelHost ?? currentHost ?? CommandPaletteHost.Instance;
    }

    public ICommandProviderContext GetProviderContextForCommand(object? command, ICommandProviderContext? currentContext)
    {
        ICommandProviderContext? topLevelId = null;
        if (command is TopLevelViewModel topLevelViewModel)
        {
            topLevelId = topLevelViewModel.ProviderContext;
        }

        return topLevelId ?? currentContext ?? throw new InvalidOperationException("No command provider context could be found for the given command, and no current context was provided.");
    }
}
