// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A <see cref="TopLevelViewModel"/> for a fallback command item. Holds the
/// fallback-specific state (fallback ID, display title, per-fallback enabled
/// setting) and forwards query updates to the extension's fallback handler.
/// </summary>
internal sealed partial class TopLevelFallbackViewModel : TopLevelViewModel
{
    private readonly ExtensionObject<IFallbackCommandItem> _model;
    private readonly string _fallbackId = string.Empty;

    // Keeps recording a render and comparing an update atomic with respect to each other.
    private readonly Lock _renderedTextLock = new();

    // Fallback commands leave matching replies to queries as an exercise for the caller.
    // Compare updates against title/subtitle pairs rendered since the last refresh request.
    // A render can catch a partial or superseded update, and the last pair may come from
    // a discarded fetch. All hidden pairs count as one.
    private readonly List<RenderedText> _renderedTexts = [];

    private string _displayTitle = string.Empty;

    protected override string IdFromModel => string.IsNullOrWhiteSpace(_fallbackId) ? base.IdFromModel : _fallbackId;

    public override bool IsFallback => true;

    public override string DisplayTitle => _displayTitle;

    public override bool IsEnabled => !ProviderSettings.FallbackCommands.TryGetValue(_fallbackId, out var settings) || settings.IsEnabled;

    internal TopLevelFallbackViewModel(
        CommandItemViewModel item,
        CommandPaletteHost extensionHost,
        ICommandProviderContext commandProviderContext,
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        IFallbackCommandItem? model,
        IContextMenuFactory? contextMenuFactory)
        : base(item, TopLevelType.Fallback, extensionHost, commandProviderContext, providerSettings, serviceProvider, contextMenuFactory)
    {
        _model = new(model);

        // RPC to check type
        if (model is IFallbackCommandItem2 fallback2)
        {
            _fallbackId = fallback2.Id;
        }
    }

    internal override void InitializeProperties()
    {
        base.InitializeProperties();

        // RPC
        if (_model.Unsafe is { } model)
        {
            _displayTitle = model.DisplayTitle;
        }

        UpdateInitialIcon(false);
    }

    internal override bool SafeUpdateFallbackTextSynchronous(string newQuery, bool reportRankChanges, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            return UnsafeUpdateFallbackSynchronous(newQuery, reportRankChanges, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }

        return false;
    }

    /// <summary>
    /// Calls UpdateQuery on our fallback handler. This does RPC work, so make
    /// sure you're calling it on a BG thread.
    /// </summary>
    /// <param name="newQuery">The new search text to pass to the extension</param>
    /// <param name="reportRankChanges">true to also report title or subtitle changes of a shown fallback, which rank a global fallback; false to report only showing or hiding</param>
    /// <param name="cancellationToken">Cancelled when a newer query supersedes this one</param>
    /// <returns>true if the main list needs a refresh for this call</returns>
    private bool UnsafeUpdateFallbackSynchronous(string newQuery, bool reportRankChanges, CancellationToken cancellationToken)
    {
        if (_model.Unsafe is not { } fallback)
        {
            return false;
        }

        // RPC for method
        fallback.FallbackHandler.UpdateQuery(newQuery);

        // Guard against a stale refresh if the COM call returned after cancellation. The newer
        // query's update compares against the rendered text, so it still reports this change.
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        lock (_renderedTextLock)
        {
            // Nothing rendered since the last request (or ever): the pending render reads the live text.
            if (_renderedTexts.Count == 0)
            {
                return false;
            }

            var newText = new RenderedText(Title, Subtitle);
            var needsRefresh = false;
            foreach (var renderedText in _renderedTexts)
            {
                // - Global fallbacks are ranked by their live title and subtitle, so a change like
                //   "server01" -> "server02" can move them.
                // - Other fallbacks keep a fixed position and update their text in place, so only showing or hiding them
                //   needs a refresh. Reporting every change for them makes each keystroke's completions
                //   postpone the typed results.
                needsRefresh = reportRankChanges
                    ? !renderedText.RendersSame(newText)
                    : renderedText.IsHidden() != newText.IsHidden();
                if (needsRefresh)
                {
                    break;
                }
            }

            // A requested refresh renders the live text, so start over. Otherwise every render
            // agreed with the new text, and it stands in for all of them.
            _renderedTexts.Clear();
            if (!needsRefresh)
            {
                _renderedTexts.Add(newText);
            }

            return needsRefresh;
        }
    }

    /// <summary>
    /// Reads the title and subtitle the main list renders this fallback with and records them in the
    /// same locked step, so a later update compares against exactly the text the render decided on.
    /// The render must use the returned text to show, hide, and rank the fallback, not read
    /// <see cref="TopLevelViewModel.Title"/> or <see cref="TopLevelViewModel.Subtitle"/> again.
    /// </summary>
    internal (string Title, string Subtitle) ReadTextForRender()
    {
        lock (_renderedTextLock)
        {
            var text = new RenderedText(Title, Subtitle);
            if (!HasRendered(text))
            {
                _renderedTexts.Add(text);
            }

            return (text.Title, text.Subtitle);
        }
    }

    private bool HasRendered(RenderedText text)
    {
        foreach (var renderedText in _renderedTexts)
        {
            if (renderedText.RendersSame(text))
            {
                return true;
            }
        }

        return false;
    }

    // The title and subtitle one render showed this fallback with.
    private readonly record struct RenderedText(string Title, string Subtitle)
    {
        public bool IsHidden() => string.IsNullOrWhiteSpace(Title);

        // A hidden fallback renders nothing, so its subtitle can't change the list.
        public bool RendersSame(RenderedText other) => IsHidden() ? other.IsHidden() : this == other;
    }
}
