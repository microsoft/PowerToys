// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

internal sealed class PowerToysAppHostService : IAppHostService
{
    public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost)
    {
        AppExtensionHost? topLevelHost = null;
        if (context is TopLevelViewModel topLevelViewModel)
        {
            topLevelHost = topLevelViewModel.ExtensionHost;
        }

        return topLevelHost ?? currentHost ?? CommandPaletteHost.Instance;
    }
}
