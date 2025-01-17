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
public partial class TopLevelCommandItemWrapper : ListItem
{
    public ExtensionObject<ICommandItem> Model { get; }

    public bool IsFallback { get; private set; }

    public string Id { get; private set; } = string.Empty;

    // public override ICommand? Command { get => base.Command; set => base.Command = value; }
    private readonly TopLevelCommandWrapper _topLevelCommand;

    public CommandPaletteHost? ExtensionHost { get => _topLevelCommand.ExtensionHost; set => _topLevelCommand.ExtensionHost = value; }

    public TopLevelCommandItemWrapper(ExtensionObject<ICommandItem> commandItem, bool isFallback)
        : base(new TopLevelCommandWrapper(commandItem.Unsafe?.Command ?? new NoOpCommand()))
    {
        _topLevelCommand = (TopLevelCommandWrapper)this.Command!;

        IsFallback = isFallback;

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

            _topLevelCommand.UnsafeInitializeProperties();

            Id = _topLevelCommand.Id;

            Title = model.Title;
            Subtitle = model.Subtitle;
            Icon = model.Icon;
            MoreCommands = model.MoreCommands;

            model.PropChanged += Model_PropChanged;
            _topLevelCommand.PropChanged += Model_PropChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void Model_PropChanged(object sender, PropChangedEventArgs args)
    {
        try
        {
            var propertyName = args.PropertyName;
            var model = Model.Unsafe;
            if (model == null)
            {
                return; // throw?
            }

            switch (propertyName)
            {
                case nameof(_topLevelCommand.Name):
                case nameof(Title):
                    this.Title = model.Title;
                    break;
                case nameof(Subtitle):
                    this.Subtitle = model.Subtitle;
                    break;
                case nameof(Icon):
                    var listIcon = model.Icon;
                    Icon = model.Icon;
                    break;

                    // TODO! MoreCommands array, which needs to also raise HasMoreCommands
            }
        }
        catch
        {
        }
    }

    public void TryUpdateFallbackText(string newQuery)
    {
        if (!IsFallback)
        {
            return;
        }

        try
        {
            _ = Task.Run(() =>
            {
                var model = Model.Unsafe;
                if (model is IFallbackCommandItem fallback)
                {
                    fallback.FallbackHandler.UpdateQuery(newQuery);
                }
            });
        }
        catch (Exception)
        {
        }
    }
}
