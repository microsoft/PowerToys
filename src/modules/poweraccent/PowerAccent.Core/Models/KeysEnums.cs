using Vanara.PInvoke;

namespace PowerAccent.Core;

public enum LetterKey
{
    A = User32.VK.VK_A,
    C = User32.VK.VK_C,
    E = User32.VK.VK_E,
    I = User32.VK.VK_I,
    O = User32.VK.VK_O,
    U = User32.VK.VK_U,
    Y = User32.VK.VK_Y
}

public enum TriggerKey
{
    Left = User32.VK.VK_LEFT,
    Right = User32.VK.VK_RIGHT,
    Space = User32.VK.VK_SPACE
}
