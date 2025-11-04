// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class BindTransformers
{
    public static bool Negate(bool value) => !value;

    public static Visibility EmptyToCollapsed(string? input)
        => string.IsNullOrEmpty(input) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility EmptyOrWhitespaceToCollapsed(string? input)
        => string.IsNullOrWhiteSpace(input) ? Visibility.Collapsed : Visibility.Visible;
}
