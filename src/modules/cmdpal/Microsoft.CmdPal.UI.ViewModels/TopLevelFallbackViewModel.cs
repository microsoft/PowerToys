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

    protected override string IdFromModel => string.IsNullOrWhiteSpace(_fallbackId) ? base.IdFromModel : _fallbackId;

    public override bool IsFallback => true;

    public override string DisplayTitle => _displayTitle;

    public override bool IsEnabled => IsEnabledWithSettings(ProviderSettings);

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

    internal override bool SafeUpdateFallbackTextSynchronous(string newQuery)
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            return UnsafeUpdateFallbackSynchronous(newQuery);
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
    /// <returns>true if our Title changed across this call</returns>
    private bool UnsafeUpdateFallbackSynchronous(string newQuery)
    {
        if (_model.Unsafe is not { } fallback)
        {
            return false;
        }

        var oldTitle = Title;

        // RPC for method
        fallback.FallbackHandler.UpdateQuery(newQuery);
        var newTitle = Title;

        // Report any title change, not just an empty <-> non-empty flip: the render path
        // re-scores fallbacks off this signal, so a change like "server01" -> "server02"
        // must still trigger a refresh or the fallback keeps its stale score and position.
        return !string.Equals(oldTitle, newTitle, StringComparison.Ordinal);
    }

    internal bool IsEnabledWithSettings(ProviderSettings providerSettings)
    {
        return !providerSettings.FallbackCommands.TryGetValue(_fallbackId, out var settings) || settings.IsEnabled;
    }
}
