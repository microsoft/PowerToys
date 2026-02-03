// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Encapsulates a navigation request within Command Palette view models.
/// </summary>
/// <param name="TargetViewModel">A view model that should be navigated to.</param>
/// <param name="NavigationToken"> A <see cref="CancellationToken"/> that can be used to cancel the pending navigation.</param>
public record AsyncNavigationRequest(object? TargetViewModel, CancellationToken NavigationToken);

#pragma warning disable SA1402 // File may only contain a single type

public record AsyncListPageNavigationRequest(object? TargetViewModel, SettingsService SettingsService, ILogger Logger, CancellationToken NavigationToken)
    : AsyncNavigationRequest(TargetViewModel, NavigationToken);

public record AsyncContentPageNavigationRequest(object? TargetViewModel, object? ImageProvider, CancellationToken NavigationToken)
    : AsyncNavigationRequest(TargetViewModel, NavigationToken);

#pragma warning restore SA1402 // File may only contain a single type
