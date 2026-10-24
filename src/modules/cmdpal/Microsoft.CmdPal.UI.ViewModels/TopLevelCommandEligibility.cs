// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class TopLevelCommandEligibility
{
    internal static bool IsEligibleForHome(TopLevelViewModel command) =>
        IsEligibleForHome(command.IsFallback, command.Title);

    internal static bool IsEligibleForHome(bool isFallback, string? title) =>
        !isFallback && !string.IsNullOrEmpty(title);
}
