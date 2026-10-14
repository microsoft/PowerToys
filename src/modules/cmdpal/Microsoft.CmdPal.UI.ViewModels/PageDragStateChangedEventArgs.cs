// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class PageDragStateChangedEventArgs(bool isDragging) : EventArgs
{
    public bool IsDragging { get; } = isDragging;
}
