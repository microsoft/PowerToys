// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Common.Deferred;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// See <see cref="IconBox.SourceRequested"/> event.
/// </summary>
public class SourceRequestedEventArgs(object? key, ElementTheme requestedTheme) : DeferredEventArgs
{
    public object? Key { get; private set; } = key;

    public IconSource? Value { get; set; }

    public ElementTheme Theme => requestedTheme;
}
