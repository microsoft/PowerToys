// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CmdPal.UI.Helpers;

public class LocalKeyboardListenerKeyPressedEventArgs(VirtualKey key) : EventArgs
{
    public VirtualKey Key { get; } = key;
}
