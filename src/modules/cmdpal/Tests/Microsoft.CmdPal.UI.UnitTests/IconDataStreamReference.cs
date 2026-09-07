// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class IconDataStreamReference
{
    public IRandomAccessStreamReference? Unsafe { get; init; }
}
