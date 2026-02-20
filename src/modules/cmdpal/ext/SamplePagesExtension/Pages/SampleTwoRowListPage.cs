// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleTwoRowListPage : SampleListPageWithItems
{
    public SampleTwoRowListPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "Sample Two-Line List Page";
        GridProperties = new MediumListLayout();
    }
}
