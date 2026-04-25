// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;
#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// A sample dock band with one button.
/// Clicking on this button will open the palette to the samples list page
/// </summary>
internal sealed partial class SampleDockBand : WrappedDockItem
{
    public SampleDockBand()
        : base(new SamplesListPage(), "Command Palette Samples")
    {
    }
}

#pragma warning restore SA1402 // File may only contain a single type
