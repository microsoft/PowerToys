// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// A text box that takes the injector's SendInput calls as the system would with the engine's hook installed: each key
/// event goes through the hook's own handling first, and the text box gets what it lets through. Its text, and the caret
/// as an index into it.
/// </summary>
internal sealed class FakeTextBox : IDisposable
{
    // How deep in the hook's handling this thread is.
    [ThreadStatic]
    private static int _inHook;

    private readonly ScratchDirectory? _scratch;
    private readonly SnippetStore? _store;
    private bool _ctrl;

    /// <summary>Takes key events through <paramref name="hook"/>, or through a hook of its own when it's null.</summary>
    public FakeTextBox(string text, KeyboardHook? hook = null)
    {
        Text = text;
        Caret = text.Length;
        if (hook is null)
        {
            _scratch = ScratchDirectory.Create();
            _store = new SnippetStore(Path.Combine(_scratch.Path, "snippets.json"));
            hook = new KeyboardHook(_store);
        }

        Hook = hook;
        Hook.Guard.Resend = Dispatch;
        Hook.Guard.TargetInFront = _ =>
        {
            AssertOutsideTheHook();
            return InFront;
        };
    }

    public KeyboardHook Hook { get; }

    public InsertionGuard Guard => Hook.Guard;

    /// <summary>Gets or sets a value indicating whether the box is the window in front, for the guard.</summary>
    public bool InFront { get; set; } = true;

    /// <summary>
    /// Gets or sets a key Keyboard Manager remaps, and what to, after our hook, which usually runs first: it swallows the
    /// key, and injects the one it's remapped to.
    /// </summary>
    public (int From, int To)? Remap { get; set; }

    public string Text { get; private set; }

    public int Caret { get; set; }

    /// <summary>Gets or sets what Ctrl+V pastes.</summary>
    public string Clipboard { get; set; } = string.Empty;

    /// <summary>Gets each SendInput call: the injector's, and the guard's sending held keys back.</summary>
    public List<Native.INPUT[]> Batches { get; } = [];

    /// <summary>Gets or sets what happens before each call is taken, given how many came before: the user typing, say.</summary>
    public Action<int>? BeforeCall { get; set; }

    /// <summary>Gets or sets what happens after each key event reaches the box.</summary>
    public Action<Native.KBDLLHOOKSTRUCT>? AfterKey { get; set; }

    /// <summary>A key event as the hook sees it when it was sent this way.</summary>
    public static Native.KBDLLHOOKSTRUCT ToHookEvent(Native.INPUT input)
    {
        Native.KEYBDINPUT key = input.U.Ki;
        bool unicode = (key.DwFlags & Native.KEYEVENTF_UNICODE) != 0;
        return new Native.KBDLLHOOKSTRUCT
        {
            VkCode = unicode ? (uint)Native.VK_PACKET : key.WVk,
            ScanCode = key.WScan,
            Flags = ((key.DwFlags & Native.KEYEVENTF_KEYUP) != 0 ? Native.LLKHF_UP : 0)
                | ((key.DwFlags & Native.KEYEVENTF_EXTENDEDKEY) != 0 ? Native.LLKHF_EXTENDED : 0)
                | 0x10, // LLKHF_INJECTED
            DwExtraInfo = key.DwExtraInfo,
        };
    }

    /// <summary>A character the user typed on the touch keyboard, as the hook sees it: untagged.</summary>
    public static Native.KBDLLHOOKSTRUCT UserKey(char ch, bool up) => new() { VkCode = Native.VK_PACKET, ScanCode = ch, Flags = up ? Native.LLKHF_UP : 0 };

    /// <summary>A key the user pressed or let go of, as the hook sees it.</summary>
    public static Native.KBDLLHOOKSTRUCT UserVk(int vk, bool up) => new() { VkCode = (uint)vk, Flags = up ? Native.LLKHF_UP : 0 };

    /// <summary>The marker of the insertion with id <paramref name="id"/>, as the hook sees it.</summary>
    public static Native.KBDLLHOOKSTRUCT Marker(byte id) => ToHookEvent(InsertionGuard.Marker(id));

    /// <summary>A batch as keys: a press as its name or character, a lone key-down or key-up marked as such.</summary>
    public static string Describe(Native.INPUT[] batch)
    {
        var keys = new List<string>();
        for (int i = 0; i < batch.Length; i++)
        {
            Native.KEYBDINPUT key = batch[i].U.Ki;
            if (key.WVk == InsertionGuard.MarkerVk)
            {
                keys.Add("Mark");
                continue;
            }

            string name = (key.DwFlags & Native.KEYEVENTF_UNICODE) != 0 ? ((char)key.WScan).ToString() : key.WVk switch
            {
                Native.VK_BACK => "BS",
                Native.VK_CONTROL => "Ctrl",
                Native.VK_LCONTROL => "LCtrl",
                Native.VK_LSHIFT => "LShift",
                Native.VK_RETURN => "Enter",
                Native.VK_LEFT => "Left",
                Native.VK_RIGHT => "Right",
                _ => ((char)key.WVk).ToString(),
            };

            bool up = (key.DwFlags & Native.KEYEVENTF_KEYUP) != 0;
            Native.KEYBDINPUT? next = i + 1 < batch.Length ? batch[i + 1].U.Ki : null;
            if (!up && next is { } n && n.WVk == key.WVk && n.WScan == key.WScan && (n.DwFlags & Native.KEYEVENTF_KEYUP) != 0)
            {
                keys.Add(name);
                i++;
            }
            else
            {
                keys.Add(name + (up ? "-up" : "-down"));
            }
        }

        return string.Join(" ", keys);
    }

    public void Insert(string value)
    {
        Text = Text.Insert(Caret, value);
        Caret += value.Length;
    }

    /// <summary>The user types <paramref name="value"/> on the touch keyboard.</summary>
    public void UserTypes(string value)
    {
        foreach (char ch in value)
        {
            Take(UserKey(ch, up: false));
            Take(UserKey(ch, up: true));
        }
    }

    /// <summary>The user presses or lets go of keys, in order.</summary>
    public void UserPresses(params (int Vk, bool Up)[] keys)
    {
        foreach ((int vk, bool up) in keys)
        {
            Take(UserVk(vk, up));
        }
    }

    /// <summary>Takes a SendInput call: keeps a copy, as the caller may clear its own, and takes its key events.</summary>
    public uint Dispatch(Native.INPUT[] inputs)
    {
        AssertOutsideTheHook();
        BeforeCall?.Invoke(Batches.Count);
        Batches.Add([.. inputs]);
        foreach (Native.INPUT input in inputs)
        {
            Take(ToHookEvent(input));
        }

        return (uint)inputs.Length;
    }

    /// <summary>
    /// Pastes as the injector's clipboard path does: the user's clipboard goes back before the end marker. The box takes
    /// the paste at once, so nothing settles and <paramref name="keepAlive"/> isn't needed.
    /// </summary>
    public PasteOutcome Paste(string value, IntPtr targetWindow, Func<(bool Whole, bool SentAny)> send, Action keepAlive)
    {
        string saved = Clipboard;
        Clipboard = value;
        try
        {
            (bool whole, bool sentAny) = send();
            return !sentAny ? PasteOutcome.TargetLost : whole ? PasteOutcome.Pasted : PasteOutcome.FailedAfterErase;
        }
        finally
        {
            Clipboard = saved;
        }
    }

    /// <summary>Takes a key event as the system would: the hook first, then the text box.</summary>
    public void Take(Native.KBDLLHOOKSTRUCT key)
    {
        bool down = (key.Flags & Native.LLKHF_UP) == 0;
        bool swallowed;
        _inHook++;
        try
        {
            swallowed = Hook.OnKeyEvent(key, down ? Native.WM_KEYDOWN : 0x0101);
        }
        finally
        {
            _inHook--;
        }

        if (swallowed)
        {
            return;
        }

        if (Remap is (int from, int to) && key.VkCode == from)
        {
            Take(key with { VkCode = (uint)to, Flags = key.Flags | 0x10, DwExtraInfo = 0x11 });
            return;
        }

        if (key.VkCode == Native.VK_PACKET)
        {
            if (down)
            {
                Insert(((char)key.ScanCode).ToString());
            }
        }
        else if (key.VkCode is Native.VK_CONTROL or Native.VK_LCONTROL or Native.VK_RCONTROL)
        {
            _ctrl = down;
        }
        else if (down)
        {
            switch (key.VkCode)
            {
                case Native.VK_BACK:
                    Caret--;
                    Text = Text.Remove(Caret, 1);
                    break;
                case Native.VK_LEFT:
                    Caret--;
                    break;
                case Native.VK_RIGHT:
                    Caret++;
                    break;
                case Native.VK_RETURN:
                    Insert("\n");
                    break;
                case Native.VK_V when _ctrl:
                    Insert(Clipboard);
                    break;
            }
        }

        AfterKey?.Invoke(key);
    }

    /// <summary>Shows the caret as "|".</summary>
    public override string ToString() => Text.Insert(Caret, "|");

    public void Dispose()
    {
        if (_store is not null)
        {
            Hook.Dispose();
            _store.Dispose();
            _scratch!.Dispose();
        }
    }

    /// <summary>
    /// The hook never sends input or asks about a window: SendInput waits on every hook, so sent from ours it nested,
    /// and a slow call could get ours removed.
    /// </summary>
    private static void AssertOutsideTheHook() => Assert.AreEqual(0, _inHook, "Called from inside the hook.");
}
