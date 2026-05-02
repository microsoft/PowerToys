// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using PowerToys.MacroCommon.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace PowerToys.MacroEngine;

internal sealed class HotkeyManager : IDisposable
{
    private readonly Dictionary<int, string> _idToMacroId = [];
    private HotkeyWindow? _window;
    private Thread? _thread;
    private int _nextId = 1;
    private int _disposed;

    public event EventHandler<string>? HotkeyTriggered;

    public void Start()
    {
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _window = new HotkeyWindow();
            _window.HotkeyPressed += (_, id) =>
            {
                if (_idToMacroId.TryGetValue(id, out var macroId))
                {
                    HotkeyTriggered?.Invoke(this, macroId);
                }
            };
            ready.Set();
            Application.Run(_window);
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
    }

    public void RegisterHotkey(MacroHotkeySettings hotkey, string macroId)
    {
        ArgumentNullException.ThrowIfNull(hotkey, nameof(hotkey));
        ArgumentException.ThrowIfNullOrWhiteSpace(macroId, nameof(macroId));

        if (_window is null)
        {
            throw new InvalidOperationException("Call Start() before RegisterHotkey().");
        }

        uint mods = (hotkey.Ctrl  ? KeyParser.ModControl : 0u)
                  | (hotkey.Alt   ? KeyParser.ModAlt     : 0u)
                  | (hotkey.Shift ? KeyParser.ModShift   : 0u)
                  | (hotkey.Win   ? KeyParser.ModWin     : 0u)
                  | KeyParser.ModNoRepeat;

        _window.Invoke(() =>
        {
            int id = _nextId++;
            if (!PInvoke.RegisterHotKey(
                    (HWND)_window.Handle,
                    id,
                    (HOT_KEY_MODIFIERS)mods,
                    (uint)hotkey.Code))
            {
                throw new InvalidOperationException(
                    $"RegisterHotKey failed for macro '{macroId}' (VK=0x{hotkey.Code:X2}). It may conflict with another application.");
            }

            _idToMacroId[id] = macroId;
        });
    }

    public void UnregisterAll()
    {
        if (_window is null)
        {
            return;
        }

        _window.Invoke(() =>
        {
            foreach (var id in _idToMacroId.Keys)
            {
                PInvoke.UnregisterHotKey((HWND)_window.Handle, id);
            }

            _idToMacroId.Clear();
            _nextId = 1;
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        UnregisterAll();
        _window?.BeginInvoke(() => Application.ExitThread());
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
    }

    private sealed class HotkeyWindow : Form
    {
        // Windows message posted when a registered hotkey fires.
        private const int WmHotkey = 0x0312;

        public event EventHandler<int>? HotkeyPressed;

        public HotkeyWindow()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.None;
            _ = Handle; // Force handle creation
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                HotkeyPressed?.Invoke(this, m.WParam.ToInt32());
            }

            base.WndProc(ref m);
        }
    }
}
