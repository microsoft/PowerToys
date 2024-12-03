// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Abstraction of a top-level command. Currently owns just a live ICommandItem
/// from an extension (or in-proc command provider), but in the future will
/// also support stub top-level items.
/// </summary>
public partial class TopLevelCommandWrapper : ListItem
{
    public ExtensionObject<ICommandItem> Model { get; }

    public TopLevelCommandWrapper(ExtensionObject<ICommandItem> commandItem)
        : base(commandItem.Unsafe?.Command ?? new NoOpCommand())
    {
        // TODO: In reality, we should do an async fetch when we're created
        // from an extension object. Probably have an
        // `static async Task<TopLevelCommandWrapper> FromExtension(ExtensionObject<ICommandItem>)`
        // or a
        // `async Task PromoteStub(ExtensionObject<ICommandItem>)`
        Model = commandItem;
        try
        {
            var model = Model.Unsafe;
            if (model == null)
            {
                return;
            }

            Title = model.Title;
            Subtitle = model.Subtitle;
            Icon = new(model.Icon.Icon);
            MoreCommands = model.MoreCommands;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
