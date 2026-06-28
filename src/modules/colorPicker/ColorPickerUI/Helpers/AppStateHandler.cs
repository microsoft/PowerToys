// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Common.UI;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

using static ColorPicker.Helpers.NativeMethodsHelper;

namespace ColorPicker.Helpers
{
    public class ColorEntry
    {
        public string ColorCode { get; set; }
        public bool IsPinned { get; set; }
    }

    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        // --- History Management ---
        private static readonly List<ColorEntry> _colorHistory = new List<ColorEntry>();

        public static void AddToHistory(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return;
            var existing = _colorHistory.FirstOrDefault(c => c.ColorCode == color);
            if (existing != null)
            {
                _colorHistory.Remove(existing);
                _colorHistory.Insert(0, existing);
                return;
            }
            _colorHistory.Insert(0, new ColorEntry { ColorCode = color, IsPinned = false });
            if (_colorHistory.Count > 10)
            {
                var lastNonPinned = _colorHistory.LastOrDefault(c => !c.IsPinned);
                if (lastNonPinned != null) _colorHistory.Remove(lastNonPinned);
            }
        }

        public static void TogglePin(string color)
        {
            var entry = _colorHistory.FirstOrDefault(c => c.ColorCode == color);
            if (entry != null) entry.IsPinned = !entry.IsPinned;
        }

        public static List<ColorEntry> GetHistory() => _colorHistory.ToList();
        // --------------------------

        private readonly IColorEditorViewModel _colorEditorViewModel;
        private readonly IUserSettings _userSettings;
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private Lock _colorPickerVisibilityLock = new Lock();

        private HwndSource _hwndSource;
        private const int _globalHotKeyId = 0x0001;

        public static bool BlockEscapeKeyClosingColorPickerEditor { get; set; }

        [ImportingConstructor]
        public AppStateHandler(IColorEditorViewModel colorEditorViewModel, IUserSettings userSettings)
        {
            Application.Current.MainWindow.Closed += MainWindow_Closed;
            _colorEditorViewModel = colorEditorViewModel;
            _userSettings = userSettings;
        }

        public event EventHandler AppShown;
        public event EventHandler AppHidden;
        public event EventHandler AppClosed;
        public event EventHandler EnterPressed;
        public event EventHandler UserSessionStarted;
        public event EventHandler UserSessionEnded;

        public void StartUserSession()
        {
            EndUserSession();
            lock (_colorPickerVisibilityLock)
            {
                if (!_colorPickerShown && !IsColorPickerEditorVisible())
                {
                    SessionEventHelper.Start(_userSettings.ActivationAction.Value);
                }

                if (_userSettings.ActivationAction.Value == ColorPickerActivationAction.OpenEditor)
                {
                    ShowColorPickerEditor();
                }
                else
                {
                    ShowColorPicker();
                }

                if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                {
                    UserSessionStarted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool EndUserSession()
        {
            lock (_colorPickerVisibilityLock)
            {
                if (IsColorPickerEditorVisible() || _colorPickerShown)
                {
                    if (IsColorPickerEditorVisible()) HideColorPickerEditor();
                    else HideColorPicker();

                    if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                    {
                        UserSessionEnded?.Invoke(this, EventArgs.Empty);
                    }
                    SessionEventHelper.End();
                    return true;
                }
                return false;
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e) { }
        private bool IsColorPickerEditorVisible() => _colorEditorWindow != null;
        private void ShowColorPickerEditor() { }
        private void HideColorPickerEditor() { }
        private void ShowColorPicker() { }
        private void HideColorPicker() { }
    }
}
