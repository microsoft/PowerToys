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
    private readonly AppExtensionHost _defaultHost;

    public PowerToysAppHostService(ILogger<PowerToysAppHostService> logger)
    {
        // Create a minimal default host for cases where no specific command host is available
        _defaultHost = new DefaultAppExtensionHost(logger);
    }

    public AppExtensionHost GetDefaultHost()
    {
        return _defaultHost;
    }

    public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost)
    {
        AppExtensionHost? topLevelHost = null;
        if (context is TopLevelViewModel topLevelViewModel)
        {
            topLevelHost = topLevelViewModel.ExtensionHost;
        }

        return topLevelHost ?? currentHost ?? _defaultHost;
    }

    private sealed class DefaultAppExtensionHost : AppExtensionHost
    {
        public DefaultAppExtensionHost(ILogger logger)
            : base(logger)
        {
        }

        public override string? GetExtensionDisplayName() => "CmdPal";
    }
}
