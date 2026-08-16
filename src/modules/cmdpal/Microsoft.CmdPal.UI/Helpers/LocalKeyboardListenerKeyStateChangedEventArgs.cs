// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

public class LocalKeyboardListenerKeyStateChangedEventArgs(VirtualKey key, bool isDown) : EventArgs
{
    public VirtualKey Key { get; } = key;

    public bool IsDown { get; } = isDown;
}
