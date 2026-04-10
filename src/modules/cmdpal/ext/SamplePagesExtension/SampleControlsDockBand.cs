// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleControlsDockBand : WrappedDockItem
{
    public SampleControlsDockBand()
        : base(new SampleControlsPage(), "Quick Settings")
    {
        Icon = new IconInfo("\uE713");
    }
}
