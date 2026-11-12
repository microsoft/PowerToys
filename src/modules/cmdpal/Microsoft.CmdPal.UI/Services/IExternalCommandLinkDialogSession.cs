// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

/// <summary>Controls one serialized external-command dialog session.</summary>
internal interface IExternalCommandLinkDialogSession : IAsyncDisposable
{
    /// <summary>Shows the cancellable loading state.</summary>
    /// <returns>A task that completes when the dialog closes.</returns>
    Task ShowLoadingAsync();

    /// <summary>Shows consent, starting the dialog when necessary.</summary>
    /// <param name="request">Consent metadata.</param>
    /// <returns>The selected consent result.</returns>
    Task<ExternalCommandConsentResult> RequestConsentAsync(ExternalCommandConsentRequest request);

    /// <summary>Shows the unavailable state, starting the dialog when necessary.</summary>
    /// <returns>A task that completes when the dialog closes.</returns>
    Task ShowUnavailableAsync();

    /// <summary>Closes the dialog; no-op before presentation.</summary>
    /// <returns>A task that completes when the dialog closes.</returns>
    Task CloseAsync();
}
