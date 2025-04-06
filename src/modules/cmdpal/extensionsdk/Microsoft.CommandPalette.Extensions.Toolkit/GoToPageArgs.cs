// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class GoToPageArgs : IGoToPageArgs
{
    public required string PageId { get; set; }

    public NavigationMode NavigationMode { get; set; } = NavigationMode.Push;
}
