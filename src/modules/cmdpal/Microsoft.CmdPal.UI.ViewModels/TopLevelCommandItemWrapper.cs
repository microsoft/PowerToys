// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Abstraction of a top-level command. Currently owns just a live ICommandItem
/// from an extension (or in-proc command provider), but in the future will
/// also support stub top-level items.
/// </summary>
public partial class TopLevelCommandItemWrapper : ListItem
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _commandProviderId;

    public ExtensionObject<ICommandItem> Model { get; }

    public bool IsFallback { get; private set; }

    private readonly string _idFromModel = string.Empty;
    private string _generatedId = string.Empty;

    public string Id => string.IsNullOrEmpty(_idFromModel) ? _generatedId : _idFromModel;

    private readonly TopLevelCommandWrapper _topLevelCommand;

    public CommandAlias? Alias { get; private set; }

    private HotkeySettings? _hotkey;

    public HotkeySettings? Hotkey
    {
        get => _hotkey;
        set
        {
            UpdateHotkey();
            UpdateTags();
        }
    }

    public CommandPaletteHost ExtensionHost { get => _topLevelCommand.ExtensionHost; }

    public TopLevelCommandItemWrapper(
        ExtensionObject<ICommandItem> commandItem,
        bool isFallback,
        CommandPaletteHost extensionHost,
        string commandProviderId,
        IServiceProvider serviceProvider)
        : base(new TopLevelCommandWrapper(
            commandItem.Unsafe?.Command ?? new NoOpCommand(),
            extensionHost))
    {
        _serviceProvider = serviceProvider;
        _topLevelCommand = (TopLevelCommandWrapper)this.Command!;
        _commandProviderId = commandProviderId;

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

            _idFromModel = _topLevelCommand.Id;

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

        GenerateId();

        UpdateAlias();
        UpdateHotkey();
        UpdateTags();
    }

    private void GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(_commandProviderId + Title + Subtitle, seed: 0);
        _generatedId = $"{_commandProviderId}{result}";
    }

    public void UpdateAlias(CommandAlias? newAlias)
    {
        _serviceProvider.GetService<AliasManager>()!.UpdateAlias(Id, newAlias);
        UpdateAlias();
        UpdateTags();
    }

    private void UpdateAlias()
    {
        // Add tags for the alias, if we have one.
        var aliases = _serviceProvider.GetService<AliasManager>();
        if (aliases != null)
        {
            Alias = aliases.AliasFromId(Id);
        }
    }

    private void UpdateHotkey()
    {
        var settings = _serviceProvider.GetService<SettingsModel>()!;
        var hotkey = settings.CommandHotkeys.Where(hk => hk.CommandId == Id).FirstOrDefault();
        if (hotkey != null)
        {
            _hotkey = hotkey.Hotkey;
        }
    }

    private void UpdateTags()
    {
        var tags = new List<Tag>();

        if (Hotkey != null)
        {
            tags.Add(new Tag() { Text = Hotkey.ToString() });
        }

        if (Alias != null)
        {
            tags.Add(new Tag() { Text = Alias.SearchPrefix });
        }

        this.Tags = tags.ToArray();
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
                case nameof(MoreCommands):
                    this.MoreCommands = model.MoreCommands;
                    break;
                case nameof(Command):
                    this.Command = model.Command;
                    break;
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
