// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Vanara.PInvoke;

namespace PowerAccent.Core;

public enum LetterKey
{
    A = User32.VK.VK_A,
    C = User32.VK.VK_C,
    E = User32.VK.VK_E,
    I = User32.VK.VK_I,
    N = User32.VK.VK_N,
    O = User32.VK.VK_O,
    S = User32.VK.VK_S,
    U = User32.VK.VK_U,
    Y = User32.VK.VK_Y,
}

public enum TriggerKey
{
    Left = User32.VK.VK_LEFT,
    Right = User32.VK.VK_RIGHT,
    Space = User32.VK.VK_SPACE,
}
