// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconLoaderService : IAsyncDisposable
{
    ShellIconLocationCache ShellIconLocations { get; }

    bool TryLoadGlyph(
        string? iconString,
        string? fontFamily,
        Size iconSize,
        double scale,
        [MaybeNullWhen(false)] out IconSource result);

    /// <summary>Queues an icon load with its captured rendering context and requested priority.</summary>
    /// <param name="iconString">The icon string or protocol request, when available.</param>
    /// <param name="fontFamily">The font family for glyph icons, when supplied.</param>
    /// <param name="streamRef">An optional stream-backed icon.</param>
    /// <param name="iconSize">The logical icon dimensions.</param>
    /// <param name="scale">The display scale used to calculate pixel dimensions.</param>
    /// <param name="context">The surface theme and contrast captured before queuing.</param>
    /// <param name="tcs">Receives the loaded source or a load failure for accepted work.</param>
    /// <param name="priority">The requested work priority.</param>
    /// <param name="diagnostics">Optional measurements for the load.</param>
    /// <param name="demand">Optional current consumer demand used by the work queue.</param>
    /// <returns>True when accepted; false when the queue cannot accept the work and the caller must handle rejection.</returns>
    bool TryEnqueueLoad(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        IconRenderContext context,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority,
        IconLoadMeasurement? diagnostics = null,
        IconLoadDemand? demand = null);

    bool TryEnqueueShellItemLoad(
        ShellItemIconRequest request,
        LocatedShellIcon? locatedIcon,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority,
        IconLoadMeasurement? diagnostics = null,
        IconLoadDemand? demand = null,
        IShellItemIconLoadCoordinator? coordinator = null,
        ShellIconMeasurement shellDiagnostics = default);
}
