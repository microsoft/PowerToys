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
    private string _displayTitle = string.Empty;
    private string? _lastReportedTitle; // Tracks what we shown in the list, to detect change that requires a refresh

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

    internal override bool SafeUpdateFallbackTextSynchronous(string newQuery, bool reportTitleChanges, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            return UnsafeUpdateFallbackSynchronous(newQuery, reportTitleChanges, cancellationToken);
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
    /// <param name="reportTitleChanges">true to report any title change; false to report only showing or hiding</param>
    /// <param name="cancellationToken">Cancelled when a newer query supersedes this one</param>
    /// <returns>true if the main list needs a refresh for this call</returns>
    private bool UnsafeUpdateFallbackSynchronous(string newQuery, bool reportTitleChanges, CancellationToken cancellationToken)
    {
        if (_model.Unsafe is not { } fallback)
        {
            return false;
        }

        var oldTitle = Title;

        // RPC for method
        fallback.FallbackHandler.UpdateQuery(newQuery);
        var newTitle = Title;

        // Guard against a stale refresh if the COM call returned after cancellation
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var reportedTitle = Interlocked.Exchange(ref _lastReportedTitle, newTitle) ?? oldTitle;

        // - Global fallbacks are ranked by their live title, so a change like "server01" -> "server02"
        //   can move them.
        // - Other fallbacks keep a fixed position and update their text in place, so only showing or hiding them
        //   needs a refresh. Reporting every title change for them makes each keystroke's completions
        //   postpone the typed results.
        return reportTitleChanges
            ? !string.Equals(reportedTitle, newTitle, StringComparison.Ordinal)
            : string.IsNullOrWhiteSpace(reportedTitle) != string.IsNullOrWhiteSpace(newTitle);
    }
}
