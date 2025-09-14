// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Ext.Actions;

public partial class AgentsTestCommandsProvider : CommandProvider
{
    private readonly List<ICommandItem> _commands;

    // private readonly ScriptsTestPage _scriptsPage;
    private static ActionRuntime? _actionRuntime;
    private bool _init;

    public AgentsTestCommandsProvider()
    {
        DisplayName = "Agents for Windows";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Id = "Actions";

        // _scriptsPage = new ScriptsTestPage();
        _commands = [
            new CommandItem(new AgentsTestPage())
            {
                Title = DisplayName,
                Subtitle = "Use @ to invoke various agents",
            },

            // new CommandItem(_scriptsPage)
            // {
            //    Title = "Script commands",
            //    Subtitle = "What if we were just raycast",
            // },
        ];
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

        List<ICommandItem> commands = [.. _commands];
        //// Add the contacts list page
        // commands.Add(new CommandItem(new ContactsListPage())
        // {
        //    Title = "Contacts",
        //    Subtitle = "Browse your contacts",
        //    Icon = Icons.ContactInput,
        // });
        // var topLevelScripts = _scriptsPage.GetItems();
        // commands.InsertRange(0, topLevelScripts);
        return commands.ToArray();
    }
}
