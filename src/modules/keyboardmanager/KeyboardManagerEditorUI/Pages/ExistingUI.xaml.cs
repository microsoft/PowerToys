// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

// WinUI3 implementation of the Existing Keyboard Manager UI
namespace KeyboardManagerEditorUI.Pages
{
    public sealed partial class ExistingUI : UserControl
    {
        public class KeyboardKey
        {
            public int KeyCode { get; set; }

            public string KeyName { get; set; } = string.Empty;

            public override string ToString() => KeyName;
        }

        // Struct to hold key code and name pairs
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct KeyNamePair
        {
            public int KeyCode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string KeyName;
        }

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetKeyboardKeysList(bool isShortcut, [Out] KeyNamePair[] keyList, int maxCount);

        public List<KeyboardKey> KeysList { get; private set; } = new List<KeyboardKey>();

        public ExistingUI()
        {
            this.InitializeComponent();

            LoadKeyboardKeys();

            keyComboBox.ItemsSource = KeysList;
            keyComboBox.DisplayMemberPath = "KeyName";

            newKeyComboBox.ItemsSource = KeysList;
            newKeyComboBox.DisplayMemberPath = "KeyCode";
        }

        private void LoadKeyboardKeys()
        {
            const int MaxKeys = 300;
            KeyNamePair[] keyNamePairs = new KeyNamePair[MaxKeys];

            int count = GetKeyboardKeysList(false, keyNamePairs, MaxKeys);

            KeysList = new List<KeyboardKey>(count);
            for (int i = 0; i < count; i++)
            {
                KeysList.Add(new KeyboardKey
                {
                    KeyCode = keyNamePairs[i].KeyCode,
                    KeyName = keyNamePairs[i].KeyName,
                });
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                // button.Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
                return;
            }
        }
    }
}
