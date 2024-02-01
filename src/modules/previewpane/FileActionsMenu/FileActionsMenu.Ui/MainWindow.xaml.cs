// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui
{
    public partial class MainWindow : FluentWindow, INotifyPropertyChanged
    {
        private string[] _selectedItems;

        private bool _singleItem;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public virtual bool SingleItem
        {
            get
            {
                return _singleItem;
            }

            set
            {
                _singleItem = value;
                OnPropertyChanged();
            }
        }

        public MainWindow(string[] selectedItems)
        {
            SingleItem = selectedItems.Length == 1;

            InitializeComponent();

            // WindowStyle = WindowStyle.None;
            // AllowsTransparency = true;
            _selectedItems = selectedItems;

            // Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.None);
            ContextMenu cm = (ContextMenu)FindResource("Menu");
            cm.IsOpen = true;
            cm.Closed += (sender, args) => Close();
        }

        private void GenerateHashes(object sender, RoutedEventArgs e)
        {
            Actions.Hashes.GenerateHashes(sender, _selectedItems);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder text = new StringBuilder();

            string delimiter;

            switch (((System.Windows.Controls.MenuItem)sender).Header)
            {
                case "Newline":
                    delimiter = Environment.NewLine;
                    break;
                default:
                    delimiter = ((string)((System.Windows.Controls.MenuItem)sender).Header).Replace("\"", string.Empty);
                    break;
            }

            foreach (string filename in _selectedItems)
            {
                text.Append(filename);
                text.Append(delimiter);
            }

            text.Length -= delimiter.Length;

            Clipboard.SetText(text.ToString());
        }
    }
}
