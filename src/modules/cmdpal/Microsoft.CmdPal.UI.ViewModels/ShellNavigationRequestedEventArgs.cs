// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class ShellNavigationRequestedEventArgs(bool withAnimation, bool focusSearch) : EventArgs
{
    public bool WithAnimation { get; } = withAnimation;

    public bool FocusSearch { get; } = focusSearch;
}
