// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.Logging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

internal sealed class PowerToysAppHostService : IAppHostService
{
    private readonly AppExtensionHost _defaultAppExtensionHost;

    public PowerToysAppHostService(AppExtensionHost appExtensionHost, ILogger logger)
    {
        _defaultAppExtensionHost = appExtensionHost;
    }

    public AppExtensionHost GetDefaultHost()
    {
        return _defaultAppExtensionHost;
    }

    public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost)
    {
        AppExtensionHost? topLevelHost = null;
        if (context is TopLevelViewModel topLevelViewModel)
        {
            topLevelHost = topLevelViewModel.ExtensionHost;
        }

        return topLevelHost ?? currentHost ?? _defaultAppExtensionHost;
    }
}
