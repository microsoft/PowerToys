// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Abstraction of a top-level command. Currently owns just a live ICommandItem
/// from an extension (or in-proc command provider), but in the future will
/// also support stub top-level items.
/// </summary>
public partial class TopLevelCommandItemWrapper : ListItem
{
    private readonly IServiceProvider _serviceProvider;

    public ExtensionObject<ICommandItem> Model { get; }

    public bool IsFallback { get; private set; }

    public string Id { get; private set; } = string.Empty;

    private readonly TopLevelCommandWrapper _topLevelCommand;

    public string? Alias { get; private set; }

    public CommandPaletteHost? ExtensionHost { get => _topLevelCommand.ExtensionHost; set => _topLevelCommand.ExtensionHost = value; }

    public TopLevelCommandItemWrapper(
        ExtensionObject<ICommandItem> commandItem,
        bool isFallback,
        IServiceProvider serviceProvider)
        : base(new TopLevelCommandWrapper(commandItem.Unsafe?.Command ?? new NoOpCommand()))
    {
        _serviceProvider = serviceProvider;
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

        // Add tags for the alias, if we have one.
        var aliases = _serviceProvider.GetService<AliasManager>();
        if (aliases != null)
        {
            Alias = aliases.KeysFromId(Id);
            if (Alias is string keys)
            {
                this.Tags = [new Tag() { Text = keys }];
            }
        }
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
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

        _ = Task.Run(() =>
            {
                try
                {
                    var model = Model.Unsafe;
                    if (model is IFallbackCommandItem fallback)
                    {
                        var wasEmpty = string.IsNullOrEmpty(Title);
                        fallback.FallbackHandler.UpdateQuery(newQuery);
                        var isEmpty = string.IsNullOrEmpty(Title);
                        if (wasEmpty != isEmpty)
                        {
                            WeakReferenceMessenger.Default.Send<UpdateFallbackItemsMessage>();
                        }
                    }
                }
                catch (Exception)
                {
                }
            });
    }
}
