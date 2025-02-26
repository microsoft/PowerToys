// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace KeyboardManagerEditorUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class KeyCaptureDialog : ContentDialog
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
        private readonly HashSet<VirtualKey> _pressedKeys = new();

        public string SelectedKeys { get; private set; } = string.Empty;

        public KeyCaptureDialog()
        {
            InitializeComponent();
            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += DetectKeys;
            _timer.Start();
        }

        private void DetectKeys(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            var keyText = new List<string>();
            foreach (VirtualKey key in Enum.GetValues(typeof(VirtualKey)))
            {
                if (key == VirtualKey.None)
                {
                    continue;
                }

                var state = CoreWindow.GetForCurrentThread().GetKeyState(key);
                if ((state & CoreVirtualKeyStates.Down) != 0)
                {
                    _pressedKeys.Add(key);
                }
            }

            var modifiers = _pressedKeys
                .Where(k => IsModifierKey(k))
                .OrderBy(k => k)
                .Select(GetKeyName);

            var mainKey = _pressedKeys
                .FirstOrDefault(k => !IsModifierKey(k));

            SelectedKeys = string.Join(" + ", modifiers
                .Concat(mainKey != VirtualKey.None ? new[] { GetKeyName(mainKey) } : Array.Empty<string>()));

            KeyDisplay.Text = SelectedKeys;
        }

        private bool IsModifierKey(VirtualKey key) =>
            key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or VirtualKey.LeftWindows;

        private string GetKeyName(VirtualKey key) => key switch
        {
            VirtualKey.Control => "Ctrl",
            VirtualKey.Menu => "Alt",
            VirtualKey.Shift => "Shift",
            VirtualKey.LeftWindows => "Win",
            _ => new CultureInfo("en-US").TextInfo.ToTitleCase(key.ToString().ToLower(CultureInfo.InvariantCulture)),
        };
    }
}
