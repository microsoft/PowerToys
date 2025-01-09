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

    private readonly bool _isFallback;

    public string Id { get; private set; } = string.Empty;

    public bool IsFallback => _isFallback;

    public TopLevelCommandWrapper(ExtensionObject<ICommandItem> commandItem, bool isFallback)
        : base(commandItem.Unsafe?.Command ?? new NoOpCommand())
    {
        _isFallback = isFallback;

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

            Id = model.Command?.Id ?? string.Empty;

            Title = model.Title;
            Subtitle = model.Subtitle;
            Icon = model.Icon;
            MoreCommands = model.MoreCommands;

            model.PropChanged += Model_PropChanged;
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
        if (!_isFallback)
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
