// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Commands;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Indexer.Pages;

internal sealed partial class ActionsListContextItem : CommandContextItem
{
    private readonly string fullPath;
    private readonly List<CommandContextItem> actions = [];
    private static readonly Lock UpdateMoreCommandsLock = new();
    private static ActionRuntime actionRuntime;

    public ActionsListContextItem(string fullPath)
        : base(new NoOpCommand())
    {
        Title = Resources.Indexer_Command_Actions;
        Icon = Icons.Actions;
        RequestedShortcut = KeyChordHelpers.FromModifiers(alt: true, vkey: VirtualKey.A);
        this.fullPath = fullPath;
        UpdateMoreCommands();
    }

    public bool AnyActions() => actions.Count != 0;

    private void ActionCatalog_Changed(global::Windows.AI.Actions.Hosting.ActionCatalog sender, object args)
    {
        UpdateMoreCommands();
    }

    private void UpdateMoreCommands()
    {
        // TODO! probably should OS version check to make sure that
        // we're on an OS version that supports actions
        //
        // wait no that's weird - we're already inside of a
        // ApiInformation.IsApiContractPresent("Windows.AI.Actions.ActionsContract", 4)
        // check here.
        //
        // so I don't know why this is an invalid cast down in GetActionsForInputs
        lock (UpdateMoreCommandsLock)
        {
            try
            {
                if (actionRuntime == null)
                {
                    actionRuntime = ActionRuntimeFactory.CreateActionRuntime();
                    Task.Delay(500).Wait();
                }

                actionRuntime.ActionCatalog.Changed -= ActionCatalog_Changed;
                actionRuntime.ActionCatalog.Changed += ActionCatalog_Changed;
            }
            catch
            {
                actionRuntime = null;
            }
        }

        if (actionRuntime == null)
        {
            return;
        }

        try
        {
            var extension = System.IO.Path.GetExtension(fullPath).ToLower(CultureInfo.InvariantCulture);
            ActionEntity entity = null;
            if (extension != null)
            {
                if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                {
                    entity = actionRuntime.EntityFactory.CreatePhotoEntity(fullPath);
                }
                else if (extension == ".docx" || extension == ".doc" || extension == ".pdf" || extension == ".txt")
                {
                    entity = actionRuntime.EntityFactory.CreateDocumentEntity(fullPath);
                }
            }

            if (entity == null)
            {
                entity = actionRuntime.EntityFactory.CreateFileEntity(fullPath);
            }

            lock (actions)
            {
                actions.Clear();
                ActionEntity[] inputs = [entity];
                var actionsInstances = actionRuntime.ActionCatalog.GetActionsForInputs(inputs);
                foreach (var actionInstance in actionsInstances)
                {
                    actions.Add(new CommandContextItem(new ExecuteActionCommand(actionInstance)));
                }

                MoreCommands = [.. actions];
            }
        }
        catch (Exception e)
        {
            Logger.LogError("Error getting actions", e);
        }
    }
}
