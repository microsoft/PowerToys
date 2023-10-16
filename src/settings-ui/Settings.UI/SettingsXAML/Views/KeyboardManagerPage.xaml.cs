// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class KeyboardManagerPage : Page, IRefreshablePage
    {
        private const string PowerToyName = "Keyboard Manager";

        private readonly IFileSystemWatcher watcher;

        public KeyboardManagerViewModel ViewModel { get; }

        public KeyboardManagerPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new KeyboardManagerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, FilterRemapKeysList);

            watcher = Helper.GetFileWatcher(
                PowerToyName,
                ViewModel.Settings.Properties.ActiveConfiguration.Value + ".json",
                OnConfigFileUpdate);

            InitializeComponent();
            DataContext = ViewModel;
        }

        private void OnConfigFileUpdate()
        {
            // Note: FileSystemWatcher raise notification multiple times for single update operation.
            // Todo: Handle duplicate events either by somehow suppress them or re-read the configuration everytime since we will be updating the UI only if something is changed.
            if (ViewModel.LoadProfile())
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.NotifyFileChanged();
                });
            }
        }

        private static void CombineRemappings(List<KeysDataModel> remapKeysList, uint leftKey, uint rightKey, uint combinedKey)
        {
            // Using InvariantCulture for keys as they are internally represented as numerical values
            KeysDataModel firstRemap = remapKeysList.Find(x => uint.Parse(x.OriginalKeys, CultureInfo.InvariantCulture) == leftKey);
            KeysDataModel secondRemap = remapKeysList.Find(x => uint.Parse(x.OriginalKeys, CultureInfo.InvariantCulture) == rightKey);
            if (firstRemap != null && secondRemap != null)
            {
                if (firstRemap.NewRemapKeys == secondRemap.NewRemapKeys)
                {
                    KeysDataModel combinedRemap = new KeysDataModel
                    {
                        OriginalKeys = combinedKey.ToString(CultureInfo.InvariantCulture),
                        NewRemapKeys = firstRemap.NewRemapKeys,
                    };
                    remapKeysList.Insert(remapKeysList.IndexOf(firstRemap), combinedRemap);
                    remapKeysList.Remove(firstRemap);
                    remapKeysList.Remove(secondRemap);
                }
            }
        }

        private int FilterRemapKeysList(List<KeysDataModel> remapKeysList)
        {
            if (remapKeysList != null)
            {
                CombineRemappings(remapKeysList, (uint)VirtualKey.LeftControl, (uint)VirtualKey.RightControl, (uint)VirtualKey.Control);
                CombineRemappings(remapKeysList, (uint)VirtualKey.LeftMenu, (uint)VirtualKey.RightMenu, (uint)VirtualKey.Menu);
                CombineRemappings(remapKeysList, (uint)VirtualKey.LeftShift, (uint)VirtualKey.RightShift, (uint)VirtualKey.Shift);
                CombineRemappings(remapKeysList, (uint)VirtualKey.LeftWindows, (uint)VirtualKey.RightWindows, Helper.VirtualKeyWindows);
            }

            return 0;
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
