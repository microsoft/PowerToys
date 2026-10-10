// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

public sealed class SearchBarBackRequestedEventArgs(SearchBarBackRequestKind kind, bool fromBackspace = false) : EventArgs
{
    public SearchBarBackRequestKind Kind { get; } = kind;

    public bool FromBackspace { get; } = fromBackspace;
}
