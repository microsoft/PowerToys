// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class IconDataViewModel
{
    public string Icon { get; init; } = string.Empty;

    public string? FontFamily { get; init; }

    public IconDataStreamReference? Data { get; init; }
}
