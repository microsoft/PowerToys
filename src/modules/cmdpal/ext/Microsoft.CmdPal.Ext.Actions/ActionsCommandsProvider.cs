// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Ext.Actions;

public partial class ActionsCommandsProvider : CommandProvider
{
    private readonly List<ICommandItem> _commands;

    private static ActionRuntime? _actionRuntime;
    private bool _init;

    public ActionsCommandsProvider()
    {
        DisplayName = "Windows Actions Framework";
        Icon = IconHelpers.FromRelativePath("Assets\\Actions.png");
        Id = "Actions";

        _commands = [];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        if (!_init)
        {
            _init = true;
            if (ApiInformation.IsApiContractPresent("Windows.AI.Actions.ActionsContract", 4))
            {
                _actionRuntime = ActionRuntimeManager.InstanceAsync.GetAwaiter().GetResult();
                if (_actionRuntime != null)
                {
                    _commands.Add(new CommandItem(new ActionsTestPage(_actionRuntime))
                    {
                        Title = "Actions",
                        Subtitle = "Windows Actions Framework",
                        Icon = Icons.ActionsPng,
                    });
                }
            }
        }

        return _commands.ToArray();
    }

    public static readonly bool IsActionsFeatureEnabled = GetFeatureFlag();

    private static bool GetFeatureFlag()
    {
        var env = System.Environment.GetEnvironmentVariable("CMDPAL_ENABLE_ACTIONS_LIST");
        return !string.IsNullOrEmpty(env) &&
           (env == "1" || env.Equals("true", System.StringComparison.OrdinalIgnoreCase));
    }
}
