// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.QuickAccess.ViewModels;

namespace Microsoft.PowerToys.QuickAccess.Flyout;

internal sealed record FlyoutNavigationContext(
    LauncherViewModel LauncherViewModel,
    AllAppsViewModel AllAppsViewModel,
    IQuickAccessCoordinator Coordinator);
