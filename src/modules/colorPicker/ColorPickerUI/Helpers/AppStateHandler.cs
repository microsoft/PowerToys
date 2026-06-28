// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic; // اضافه شد برای لیست‌ها
using System.ComponentModel.Composition;
using System.Linq; // اضافه شد برای مدیریت لیست
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
    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        private readonly IColorEditorViewModel _colorEditorViewModel;
        private readonly IUserSettings _userSettings;
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private Lock _colorPickerVisibilityLock = new Lock();

        private HwndSource _hwndSource;
        private const int _globalHotKeyId = 0x0001;

        // --- مدیریت تاریخچه رنگ‌ها ---
        private static readonly List<string> _colorHistory = new List<string>();

        public static void AddToHistory(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return;
            if (_colorHistory.Contains(color)) _colorHistory.Remove(color);
            _colorHistory.Insert(0, color);
            if (_colorHistory.Count > 10) _colorHistory.RemoveAt(10);
        }

        public static List<string> GetHistory() => _colorHistory.ToList();
        // -----------------------------

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
                    if (IsColorPickerEditorVisible())
                    {
                        HideColorPickerEditor();
                    }
                    else
                    {
                        HideColorPicker();
                    }

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

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Implementation...
        }

        private bool IsColorPickerEditorVisible() => _colorEditorWindow != null;

        private void ShowColorPickerEditor() { /* Implementation... */ }
        private void HideColorPickerEditor() { /* Implementation... */ }
        private void ShowColorPicker() { /* Implementation... */ }
        private void HideColorPicker() { /* Implementation... */ }
    }
}

            
}
