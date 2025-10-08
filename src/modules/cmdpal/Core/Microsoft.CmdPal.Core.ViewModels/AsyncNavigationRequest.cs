// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels;

/// <summary>
/// Encapsulates a navigation request within Command Palette view models.
/// </summary>
/// <param name="TargetViewModel">A view model that should be navigated to.</param>
/// <param name="NavigationToken"> A <see cref="CancellationToken"/> that can be used to cancel the pending navigation.</param>
public record AsyncNavigationRequest(object? TargetViewModel, CancellationToken NavigationToken);
