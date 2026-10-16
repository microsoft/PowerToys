// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Services;

internal sealed class ExternalCommandLinkCoordinatorFactory
{
    private readonly ISettingsService _settingsService;
    private readonly IExternalCommandPermissionStore _permissionStore;
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ICmdPalProtocolActivation _protocolActivation;
    private readonly IMessenger _messenger;

    public ExternalCommandLinkCoordinatorFactory(
        ISettingsService settingsService,
        IExternalCommandPermissionStore permissionStore,
        TopLevelCommandManager topLevelCommandManager,
        ICmdPalProtocolActivation protocolActivation,
        IMessenger messenger)
    {
        _settingsService = settingsService;
        _permissionStore = permissionStore;
        _topLevelCommandManager = topLevelCommandManager;
        _protocolActivation = protocolActivation;
        _messenger = messenger;
    }

    internal ExternalCommandLinkCoordinator Create(
        ShellContentDialogHost dialogHost,
        DispatcherQueue dispatcherQueue)
    {
        var presenter = new ExternalCommandLinkPresenter(dialogHost, _protocolActivation, _messenger);
        var reloadPermission = new ExternalCommandPermission(
            ExternalCommandPermissionKey.Reload,
            ResourceLoaderInstance.GetString("ExternalCommandLink_Reload_Name"),
            ResourceLoaderInstance.GetString("ExternalCommandLink_CommandPalette_Name"));

        return new ExternalCommandLinkCoordinator(
            _settingsService,
            _permissionStore,
            _topLevelCommandManager,
            presenter,
            _messenger,
            dispatcherQueue,
            reloadPermission);
    }
}
