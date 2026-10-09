// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

internal static class Icons
{
    internal static IconInfo WebSearch { get; } = IconHelpers.FromRelativePaths("Assets\\WebSearch.light.png", "Assets\\WebSearch.dark.png");

    internal static IconInfo Search { get; } = new("\uE721");

    internal static IconInfo History { get; } = new("\uE81C");
}
