// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class FallbackQueryResultItem : ListItem, IFallbackResultItem, IPrecomputedListItem
{
    private readonly Lock _sourceItemLock = new();
    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;
    private FuzzyTargetCache _extensionNameCache;
    private Func<bool> _isCurrent = static () => false;
    private bool _usesTitleOverride;
    private bool _usesSubtitleOverride;
    private TypedEventHandler<object, IPropChangedEventArgs>? _sourceItemChangedHandler;
    private IListItem? _sourceItem;

    public FallbackQueryResultItem(
        string identity,
        IListItem sourceItem,
        AppExtensionHost extensionHost,
        ICommandProviderContext providerContext,
        string fallbackSourceId,
        string extensionName,
        string aliasText,
        bool hasAlias,
        IFallbackCommandInvocationArgs? invocationArgs,
        Func<bool> isCurrent,
        bool listenForSourceItemUpdates = false,
        string? titleOverride = null,
        string? subtitleOverride = null)
        : base()
    {
        Identity = identity;
        ExtensionHost = extensionHost;
        ProviderContext = providerContext;
        FallbackSourceId = fallbackSourceId;
        ExtensionName = extensionName;
        RefreshFromSource(sourceItem, aliasText, hasAlias, invocationArgs, isCurrent, listenForSourceItemUpdates, titleOverride, subtitleOverride);
    }

    public string FallbackSourceId { get; }

    public string ExtensionName { get; }

    public bool HasAlias { get; private set; }

    public string AliasText { get; private set; } = string.Empty;

    public AppExtensionHost ExtensionHost { get; }

    public ICommandProviderContext ProviderContext { get; }

    public IFallbackCommandInvocationArgs? InvocationArgs { get; private set; }

    public bool IsCurrent => _isCurrent();

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher) => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher) => _subtitleCache.GetOrUpdate(matcher, Subtitle);

    internal FuzzyTarget GetExtensionNameTarget(IPrecomputedFuzzyMatcher matcher) => _extensionNameCache.GetOrUpdate(matcher, ExtensionName);

    internal void RefreshFromSource(
        IListItem sourceItem,
        string aliasText,
        bool hasAlias,
        IFallbackCommandInvocationArgs? invocationArgs,
        Func<bool> isCurrent,
        bool listenForSourceItemUpdates,
        string? titleOverride,
        string? subtitleOverride)
    {
        DetachSourceItemUpdates();

        AliasText = aliasText;
        HasAlias = hasAlias;
        InvocationArgs = invocationArgs;
        _isCurrent = isCurrent;
        _usesTitleOverride = titleOverride is not null;
        _usesSubtitleOverride = subtitleOverride is not null;

        Command = sourceItem.Command;
        MoreCommands = sourceItem.MoreCommands;
        Icon = sourceItem.Icon;
        Title = titleOverride ?? sourceItem.Title;
        Subtitle = subtitleOverride ?? sourceItem.Subtitle;
        Tags = sourceItem.Tags;
        Details = sourceItem.Details;
        Section = sourceItem.Section;
        TextToSuggest = string.IsNullOrEmpty(sourceItem.TextToSuggest) ? invocationArgs?.Query ?? string.Empty : sourceItem.TextToSuggest;

        CopyDataPackage(sourceItem);
        AttachSourceItemUpdates(sourceItem, listenForSourceItemUpdates);
    }

    internal void DetachSourceItemUpdates()
    {
        lock (_sourceItemLock)
        {
            if (_sourceItem is not null && _sourceItemChangedHandler is not null)
            {
                _sourceItem.PropChanged -= _sourceItemChangedHandler;
            }

            _sourceItem = null;
            _sourceItemChangedHandler = null;
        }
    }

    private void AttachSourceItemUpdates(IListItem sourceItem, bool listenForSourceItemUpdates)
    {
        if (!listenForSourceItemUpdates)
        {
            return;
        }

        TypedEventHandler<object, IPropChangedEventArgs>? handler = null;
        var weakThis = new WeakReference<FallbackQueryResultItem>(this);
        handler = (sender, args) =>
        {
            if (!weakThis.TryGetTarget(out var target))
            {
                sourceItem.PropChanged -= handler;
                return;
            }

            target.HandleSourceItemChanged(sourceItem, args);
        };

        lock (_sourceItemLock)
        {
            _sourceItem = sourceItem;
            _sourceItemChangedHandler = handler;
        }

        sourceItem.PropChanged += handler;
    }

    private void HandleSourceItemChanged(IListItem sourceItem, IPropChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(ICommandItem.Icon):
                Icon = sourceItem.Icon;
                break;
            case nameof(ICommandItem.Command):
                Command = sourceItem.Command;
                break;
            case nameof(ICommandItem.MoreCommands):
                MoreCommands = sourceItem.MoreCommands;
                break;
            case nameof(ICommandItem.Title) when !_usesTitleOverride:
                Title = sourceItem.Title;
                break;
            case nameof(ICommandItem.Subtitle) when !_usesSubtitleOverride:
                Subtitle = sourceItem.Subtitle;
                break;
            case nameof(IListItem.Tags):
                Tags = sourceItem.Tags;
                break;
            case nameof(IListItem.Details):
                Details = sourceItem.Details;
                break;
            case nameof(IListItem.Section):
                Section = sourceItem.Section;
                break;
            case nameof(IListItem.TextToSuggest):
                TextToSuggest = sourceItem.TextToSuggest;
                break;
            case nameof(CommandItem.DataPackage):
            case nameof(CommandItem.DataPackageView):
                CopyDataPackage(sourceItem);
                break;
        }
    }

    private void CopyDataPackage(ICommandItem sourceItem)
    {
        DataPackageView = null;

        if (sourceItem is not IExtendedAttributesProvider extendedAttributesProvider)
        {
            return;
        }

        var properties = extendedAttributesProvider.GetProperties();
        if (properties?.TryGetValue(WellKnownExtensionAttributes.DataPackage, out var dataPackage) == true &&
            dataPackage is DataPackageView view)
        {
            DataPackageView = view;
        }
    }
}
