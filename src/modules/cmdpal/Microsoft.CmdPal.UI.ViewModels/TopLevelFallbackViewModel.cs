// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class TopLevelFallbackViewModel
{
    private const int MaximumRegexLength = 4096;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
    private readonly TopLevelViewModel _source;
    private readonly ExtensionObject<IFallbackCommandItem> _model;
    private readonly ExtensionObject<IFallbackCommandItem3> _fallbackV2;
    private readonly HostMatchKind _matchKind;
    private readonly string _matchValue = string.Empty;
    private readonly string _titleTemplate = string.Empty;
    private readonly string _subtitleTemplate = string.Empty;
    private string _name = string.Empty;
    private volatile bool _isCleanedUp;

    internal bool IsCleanedUp => _isCleanedUp;

    internal string Id { get; }

    internal string DisplayTitle { get; }

    internal FallbackCommandMode Mode { get; } = FallbackCommandMode.Active;

    internal bool IsV2 => _fallbackV2.Unsafe is not null;

    internal uint? SuggestedQueryDelayMilliseconds { get; }

    internal uint? SuggestedMinQueryLength { get; }

    internal string? Title { get; private set; }

    internal string? Subtitle { get; private set; }

    internal FallbackInvocationViewModel? Command { get; private set; }

    internal TopLevelFallbackViewModel(TopLevelViewModel source, IFallbackCommandItem model)
    {
        _source = source;
        _model = new(model);
        Id = (model as IFallbackCommandItem2)?.Id ?? string.Empty;
        DisplayTitle = model.DisplayTitle;
        var fallbackV2 = model as IFallbackCommandItem3;
        _fallbackV2 = new(fallbackV2);
        if (fallbackV2 is not null)
        {
            Mode = fallbackV2.Mode;
            _matchKind = fallbackV2.MatchKind;
            _matchValue = fallbackV2.MatchValue;
            _titleTemplate = fallbackV2.TitleTemplate;
            _subtitleTemplate = fallbackV2.SubtitleTemplate;
            _name = fallbackV2.Name;
            SuggestedQueryDelayMilliseconds = fallbackV2.SuggestedQueryDelayMilliseconds.ToNullableUInt32();
            SuggestedMinQueryLength = fallbackV2.SuggestedMinQueryLength.ToNullableUInt32();
            if (Mode != FallbackCommandMode.Results)
            {
                Command = CreateInvocation(null, CancellationToken.None);
            }
        }
    }

    internal bool UpdateQuery(string query, string queryId, CancellationToken queryToken)
    {
        if (IsCleanedUp || queryToken.IsCancellationRequested)
        {
            return false;
        }

        var oldTitle = _source.Title;
        _model.Unsafe!.FallbackHandler.UpdateQuery(query);
        if (queryToken.IsCancellationRequested)
        {
            return false;
        }

        SetQuery(query, queryId, queryToken);
        return !string.Equals(oldTitle, _source.Title, StringComparison.Ordinal);
    }

    internal bool PreparePassiveQuery(string query, string queryId, CancellationToken queryToken)
    {
        if (IsCleanedUp || !IsV2 || Mode != FallbackCommandMode.Passive || !_source.IsEnabled || queryToken.IsCancellationRequested)
        {
            return false;
        }

        var matches = _matchKind != HostMatchKind.Regex || MatchesRegex(query, _matchValue);
        var title = matches ? FormatText(_titleTemplate, _source.ItemViewModel.Title, query) : string.Empty;
        var subtitle = matches ? FormatText(_subtitleTemplate, _source.ItemViewModel.Subtitle, query) : string.Empty;
        var changed = !string.Equals(Title, title, StringComparison.Ordinal)
            || !string.Equals(Subtitle, subtitle, StringComparison.Ordinal);
        Title = title;
        Subtitle = subtitle;
        SetQuery(query, queryId, queryToken);
        if (changed)
        {
            _source.NotifyFallbackChanged(nameof(Title));
            _source.NotifyFallbackChanged(nameof(Subtitle));
        }

        return matches;
    }

    private void SetQuery(string query, string queryId, CancellationToken queryToken)
    {
        if (!IsV2 || Mode == FallbackCommandMode.Results)
        {
            return;
        }

        Command = CreateInvocation(
            new FallbackCommandInvocationArgs(
                query,
                queryId,
                global::Windows.System.UserProfile.GlobalizationPreferences.Languages.ToArray()),
            queryToken);
        _source.NotifyFallbackChanged(nameof(ICommandItem.Command));
    }

    private FallbackInvocationViewModel CreateInvocation(IFallbackCommandInvocationArgs? args, CancellationToken queryToken) =>
        new(this, _source, Id, _name, args, queryToken);

    internal ICommand? CreateCommand(IFallbackCommandInvocationArgs args) =>
        IsCleanedUp ? null : _fallbackV2.Unsafe?.CreateCommand(args);

    internal void UpdateName()
    {
        if (!IsCleanedUp && _fallbackV2.Unsafe is { } model)
        {
            _name = model.Name;
            Command?.UpdateName(_name);
        }
    }

    internal void Cleanup()
    {
        _isCleanedUp = true;
        Command?.SafeCleanup();
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
