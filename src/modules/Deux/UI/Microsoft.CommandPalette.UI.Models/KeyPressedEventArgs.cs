// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CommandPalette.UI.Models;

public class KeyPressedEventArgs(VirtualKey key) : EventArgs
{
    public VirtualKey Key { get; } = key;
}
