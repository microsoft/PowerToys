// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleSingleRowListPage : SampleListPageWithItems
{
    public SampleSingleRowListPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "Sample Compact List Page";
        GridProperties = new SmallListLayout
        {
            // to demonstrate the automatic wrapping behavior, we set the breakpoint to medium,
            AutomaticWrappingBreakpoint = ContentSize.Medium,
        };
    }
}
