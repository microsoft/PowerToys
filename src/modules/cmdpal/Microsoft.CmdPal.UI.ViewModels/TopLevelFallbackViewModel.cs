// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class TopLevelFallbackViewModel : TopLevelViewModel
{
    private const int MaximumRegexLength = 4096;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
    private readonly ExtensionObject<IFallbackCommandItem> _model;
    private ExtensionObject<IFallbackCommandItem3> _fallbackV2 = new(null);
    private ExtensionObject<IFallbackHandler2> _queryHandler = new(null);
    private HostMatchKind _matchKind;
    private string _matchValue = string.Empty;
    private string _titleTemplate = string.Empty;
    private string _subtitleTemplate = string.Empty;
    private string _fallbackId = string.Empty;
    private string _displayTitle = string.Empty;
    private string _name = string.Empty;
    private FallbackCommandMode _mode = FallbackCommandMode.Active;
    private uint? _suggestedQueryDelayMilliseconds;
    private uint? _suggestedMinQueryLength;
    private string? _title;
    private string? _subtitle;
    private FallbackInvocationViewModel? _invocationCommand;
    private bool _isV2;
    private bool _isMetadataInitialized;
    private volatile bool _isCleanedUp;

    internal bool IsCleanedUp => _isCleanedUp;

    protected override string IdFromModel => string.IsNullOrWhiteSpace(_fallbackId) ? base.IdFromModel : _fallbackId;

    public override bool IsFallback => true;

    public override string DisplayTitle => _displayTitle;

    public override bool IsEnabled => !ProviderSettings.FallbackCommands.TryGetValue(_fallbackId, out var settings) || settings.IsEnabled;

    internal override FallbackCommandMode FallbackMode => _mode;

    internal override bool IsFallbackV2 => _isV2;

    internal override uint? SuggestedQueryDelayMilliseconds => _suggestedQueryDelayMilliseconds;

    internal override uint? SuggestedMinQueryLength => _suggestedMinQueryLength;

    internal override IFallbackHandler2? FallbackQueryHandler => IsCleanedUp ? null : _queryHandler.Unsafe;

    public override string Title => _title ?? base.Title;

    public override string Subtitle => _subtitle ?? base.Subtitle;

    public override CommandViewModel CommandViewModel => _invocationCommand ?? base.CommandViewModel;

    internal TopLevelFallbackViewModel(
        CommandItemViewModel item,
        CommandPaletteHost extensionHost,
        ICommandProviderContext commandProviderContext,
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        IFallbackCommandItem model,
        IContextMenuFactory? contextMenuFactory)
        : base(item, TopLevelType.Fallback, extensionHost, commandProviderContext, providerSettings, serviceProvider, contextMenuFactory)
    {
        _model = new(model);
    }

    internal override void InitializeProperties()
    {
        if (IsCleanedUp)
        {
            return;
        }

        if (!_isMetadataInitialized)
        {
            // The provider loader calls this on its background worker inside its initialization try block.
            UnsafeFetchMetadata();
            _isMetadataInitialized = true;
        }

        base.InitializeProperties();
        UpdateInitialIcon(false);
    }

    private void UnsafeFetchMetadata()
    {
        var model = _model.Unsafe!;
        _fallbackId = (model as IFallbackCommandItem2)?.Id ?? string.Empty;
        _displayTitle = model.DisplayTitle;
        var fallbackV2 = model as IFallbackCommandItem3;
        _fallbackV2 = new(fallbackV2);
        _isV2 = fallbackV2 is not null;
        if (fallbackV2 is not null)
        {
            _mode = fallbackV2.Mode;
            _matchKind = fallbackV2.MatchKind;
            _matchValue = fallbackV2.MatchValue;
            _titleTemplate = fallbackV2.TitleTemplate;
            _subtitleTemplate = fallbackV2.SubtitleTemplate;
            _name = fallbackV2.Name;
            _suggestedQueryDelayMilliseconds = fallbackV2.SuggestedQueryDelayMilliseconds.ToNullableUInt32();
            _suggestedMinQueryLength = fallbackV2.SuggestedMinQueryLength.ToNullableUInt32();
            if (FallbackMode == FallbackCommandMode.Results)
            {
                _queryHandler = new(fallbackV2.QueryHandler);
            }
            else
            {
                _invocationCommand = CreateInvocation(null, CancellationToken.None);
            }
        }
    }

    internal override bool SafeUpdateFallbackTextSynchronous(string newQuery, string queryId, CancellationToken queryToken)
    {
        if (IsCleanedUp || !IsEnabled || queryToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            var oldTitle = Title;
            _model.Unsafe!.FallbackHandler.UpdateQuery(newQuery);
            if (queryToken.IsCancellationRequested)
            {
                return false;
            }

            SetQuery(newQuery, queryId, queryToken);
            return !string.Equals(oldTitle, Title, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }

        return false;
    }

    internal override bool PreparePassiveFallback(string query, string queryId, CancellationToken queryToken)
    {
        if (IsCleanedUp || !IsFallbackV2 || FallbackMode != FallbackCommandMode.Passive || !IsEnabled || queryToken.IsCancellationRequested)
        {
            return false;
        }

        var matches = _matchKind != HostMatchKind.Regex || MatchesRegex(query, _matchValue);
        var title = matches ? FormatText(_titleTemplate, ItemViewModel.Title, query) : string.Empty;
        var subtitle = matches ? FormatText(_subtitleTemplate, ItemViewModel.Subtitle, query) : string.Empty;
        var changed = !string.Equals(_title, title, StringComparison.Ordinal)
            || !string.Equals(_subtitle, subtitle, StringComparison.Ordinal);
        _title = title;
        _subtitle = subtitle;
        SetQuery(query, queryId, queryToken);
        if (changed)
        {
            NotifyItemChanged(nameof(Title));
            NotifyItemChanged(nameof(Subtitle));
        }

        return matches;
    }

    private void SetQuery(string query, string queryId, CancellationToken queryToken)
    {
        if (!IsFallbackV2 || FallbackMode == FallbackCommandMode.Results)
        {
            return;
        }

        _invocationCommand = CreateInvocation(
            new FallbackCommandInvocationArgs(
                query,
                queryId,
                global::Windows.System.UserProfile.GlobalizationPreferences.Languages.ToArray()),
            queryToken);
        NotifyItemChanged(nameof(ICommandItem.Command));
    }

    private FallbackInvocationViewModel CreateInvocation(IFallbackCommandInvocationArgs? args, CancellationToken queryToken) =>
        new(this, Id, _name, args, queryToken);

    internal ICommand? UnsafeCreateCommand(IFallbackCommandInvocationArgs args) =>
        IsCleanedUp ? null : _fallbackV2.Unsafe?.CreateCommand(args);

    protected override void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandItemViewModel.Name))
        {
            try
            {
                UnsafeFetchName();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        base.Item_PropertyChanged(sender, e);
    }

    private void UnsafeFetchName()
    {
        if (!IsCleanedUp && _fallbackV2.Unsafe is { } model)
        {
            _name = model.Name;
            _invocationCommand?.UpdateName(_name);
        }
    }

    internal override void Cleanup()
    {
        _isCleanedUp = true;
        _invocationCommand?.SafeCleanup();
        base.Cleanup();
    }

    private static string FormatText(string template, string defaultValue, string query) =>
        string.IsNullOrEmpty(template) ? defaultValue : template.Replace("{query}", query, StringComparison.Ordinal);

    // Extension patterns must match the whole query and have bounded parsing and matching costs.
    private static bool MatchesRegex(string query, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > MaximumRegexLength)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(query, $"\\A(?:{pattern})\\z", RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
