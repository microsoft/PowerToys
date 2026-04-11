// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.Controls;

public class IconCarouselItemInvokedEventArgs(int index, Uri iconUri) : EventArgs
{
    public int Index { get; } = index;

    public Uri IconUri { get; } = iconUri;
}
