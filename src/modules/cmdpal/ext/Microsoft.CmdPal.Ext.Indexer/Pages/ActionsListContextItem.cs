﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
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
        try
        {
            lock (UpdateMoreCommandsLock)
            {
                if (actionRuntime == null)
                {
                    actionRuntime = ActionRuntimeFactory.CreateActionRuntime();
                    Task.Delay(500).Wait();
                }

                actionRuntime.ActionCatalog.Changed -= ActionCatalog_Changed;
                actionRuntime.ActionCatalog.Changed += ActionCatalog_Changed;
            }

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
                foreach (var actionInstance in actionRuntime.ActionCatalog.GetActionsForInputs([entity]))
                {
                    actions.Add(new CommandContextItem(new ExecuteActionCommand(actionInstance)));
                }

                MoreCommands = [.. actions];
            }
        }
        catch
        {
            actionRuntime = null;
        }
    }
}
