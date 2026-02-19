// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconLoaderService : IAsyncDisposable
{
    void EnqueueLoad(
        string? iconString,
        string? fontFamily,
        IRandomAccessStreamReference? streamRef,
        Size iconSize,
        double scale,
        TaskCompletionSource<IconSource?> tcs,
        IconLoadPriority priority);
}
