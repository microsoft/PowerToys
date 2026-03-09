// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CmdPal.Ext.RaycastStore.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore;

public partial class RaycastStoreCommandProvider : CommandProvider
{
    private readonly RaycastGitHubClient _client = new();

    private readonly InstalledExtensionTracker _tracker = new();

    private ICommandItem[]? _commands;

    private bool _nodeJsChecked;

    public RaycastStoreCommandProvider()
    {
        DisplayName = "Raycast Extension Store";
        Id = "RaycastStore";
        Icon = Icons.RaycastIcon;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        if (_commands != null)
        {
            return _commands;
        }

        if (!_nodeJsChecked)
        {
            _nodeJsChecked = true;
            Task.Run((Func<Task?>)InitializeCommandsAsync);
            _commands = new ICommandItem[]
            {
                new ListItem(new BrowseExtensionsPage(_client, _tracker))
                {
                    Title = "Browse Raycast Extensions",
                    Subtitle = "Search and install Raycast extensions for Command Palette",
                    Icon = Icons.RaycastIcon,
                },
            };
            return _commands;
        }

        return _commands ?? Array.Empty<ICommandItem>();
    }

    private async Task InitializeCommandsAsync()
    {
        if (await NodeJsDetector.DetectAsync())
        {
            _commands = new ICommandItem[]
            {
                new ListItem(new BrowseExtensionsPage(_client, _tracker))
                {
                    Title = "Browse Raycast Extensions",
                    Subtitle = "Search and install Raycast extensions for Command Palette",
                    Icon = Icons.RaycastIcon,
                },
                new ListItem(new InstalledExtensionsPage(_tracker))
                {
                    Title = "Installed Raycast Extensions",
                    Subtitle = "Manage installed Raycast extensions",
                    Icon = Icons.InstalledIcon,
                },
            };
        }
        else
        {
            _commands = new ICommandItem[]
            {
                new ListItem(new NodeJsRequiredPage())
                {
                    Title = "Raycast Extensions (Node.js Required)",
                    Subtitle = "Node.js is needed to run Raycast extensions",
                    Icon = Icons.WarningIcon,
                },
            };
        }

        RaiseItemsChanged();
    }

    public override void InitializeWithHost(IExtensionHost host)
    {
        RaycastStoreExtensionHost.Instance.Initialize(host);
    }
}
