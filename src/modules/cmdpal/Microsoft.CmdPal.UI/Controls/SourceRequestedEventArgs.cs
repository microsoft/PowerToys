// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Common.Deferred;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// See <see cref="IconBox.SourceRequested"/> event.
/// </summary>
public class SourceRequestedEventArgs(object? key, ElementTheme requestedTheme, double scale = 1.0) : DeferredEventArgs, IIconRequestDemand
{
    private IconRequestDemandState _demandState;

    public object? Key { get; private set; } = key;

    public IconSource? Value { get; set; }

    /// <summary>
    /// Gets or sets an optional source to display while <see cref="Value"/> is being resolved.
    /// Handlers should set this before their first asynchronous suspension so the control can present it immediately.
    /// </summary>
    public IconSource? FallbackSource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this request is expected to resolve to an image.
    /// This lets image-oriented placements reject a glyph produced by a final fallback path
    /// without affecting ordinary glyph requests.
    /// </summary>
    internal bool ExpectsImageSource { get; set; }

    public ElementTheme Theme => requestedTheme;

    public double Scale => scale;

    internal IconRequestMeasurement Diagnostics { get; set; }

    void IIconRequestDemand.Attach(IconLoadDemand loadDemand) => _demandState.Attach(loadDemand);

    void IIconRequestDemand.Release() => _demandState.Release();
}
