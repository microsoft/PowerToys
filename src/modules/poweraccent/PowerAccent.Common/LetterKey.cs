// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Common;

// Mirrors the LetterKey enum defined in PowerAccentKeyboardService\KeyboardListener.idl.
// The numeric values must stay in sync with the IDL definition.
// This managed copy exists so that language mapping data in CharacterMappings.cs can be shared
// with projects (e.g. Settings UI) that do not reference the WinRT keyboard service.
public enum LetterKey
{
    None = 0x00,
    VK_0 = 0x30,
    VK_1 = 0x31,
    VK_2 = 0x32,
    VK_3 = 0x33,
    VK_4 = 0x34,
    VK_5 = 0x35,
    VK_6 = 0x36,
    VK_7 = 0x37,
    VK_8 = 0x38,
    VK_9 = 0x39,
    VK_A = 0x41,
    VK_B = 0x42,
    VK_C = 0x43,
    VK_D = 0x44,
    VK_E = 0x45,
    VK_F = 0x46,
    VK_G = 0x47,
    VK_H = 0x48,
    VK_I = 0x49,
    VK_J = 0x4A,
    VK_K = 0x4B,
    VK_L = 0x4C,
    VK_M = 0x4D,
    VK_N = 0x4E,
    VK_O = 0x4F,
    VK_P = 0x50,
    VK_Q = 0x51,
    VK_R = 0x52,
    VK_S = 0x53,
    VK_T = 0x54,
    VK_U = 0x55,
    VK_V = 0x56,
    VK_W = 0x57,
    VK_X = 0x58,
    VK_Y = 0x59,
    VK_Z = 0x5A,
    VK_PLUS = 0xBB,
    VK_COMMA = 0xBC,
    VK_PERIOD = 0xBE,
    VK_MINUS = 0xBD,
    VK_MULTIPLY_ = 0x6A,
    VK_SLASH_ = 0xBF,
    VK_DIVIDE_ = 0x6F,
    VK_BACKSLASH = 0xDC,
}
