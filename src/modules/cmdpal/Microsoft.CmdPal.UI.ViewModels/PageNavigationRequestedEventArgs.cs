// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class PageNavigationRequestedEventArgs(
    PageViewModel page,
    bool withAnimation,
    bool transientPage,
    CancellationToken cancellationToken) : EventArgs
{
    public PageViewModel Page { get; } = page;

    public bool WithAnimation { get; } = withAnimation;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public bool TransientPage { get; } = transientPage;
}
