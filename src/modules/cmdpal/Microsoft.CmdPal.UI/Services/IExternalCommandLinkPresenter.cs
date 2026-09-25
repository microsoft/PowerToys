// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

/// <summary>Presents external-command loading, consent, and unavailable states.</summary>
internal interface IExternalCommandLinkPresenter
{
    /// <summary>Acquires and configures a loading-dialog session.</summary>
    /// <returns>The session, or <see langword="null"/> when presentation is unavailable.</returns>
    Task<IExternalCommandLinkDialogSession?> PrepareLoadingAsync();

    /// <summary>Shows standalone consent.</summary>
    /// <param name="request">Consent metadata.</param>
    /// <param name="summonWindow">Whether to summon the shell first.</param>
    /// <returns>The selected consent result.</returns>
    Task<ExternalCommandConsentResult> RequestConsentAsync(
        ExternalCommandConsentRequest request,
        bool summonWindow = true);

    /// <summary>Shows standalone command-unavailable UI.</summary>
    /// <param name="summonWindow">Whether to summon the shell first.</param>
    /// <returns>A task that completes when the dialog closes or presentation is unavailable.</returns>
    Task ShowUnavailableAsync(bool summonWindow = true);
}
