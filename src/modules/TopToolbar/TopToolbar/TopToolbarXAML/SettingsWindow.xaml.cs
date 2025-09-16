// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TopToolbar.Models;
using TopToolbar.Services;
using TopToolbar.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TopToolbar
{
    public sealed partial class SettingsWindow : WinUIEx.WindowEx
    {
        private readonly SettingsViewModel _vm;

        public SettingsViewModel ViewModel => _vm;

        public SettingsWindow()
        {
            this.InitializeComponent();
            _vm = new SettingsViewModel(new ToolbarConfigService());
            this.Activated += async (s, e) =>
            {
                if (_vm.Groups.Count == 0)
                {
                    await _vm.LoadAsync(this.DispatcherQueue);
                }
            };
        }

        private void OnAddGroup(object sender, RoutedEventArgs e)
        {
            _vm.AddGroup();
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            await _vm.SaveAsync();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnAddButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.AddButton(_vm.SelectedGroup);
            }
        }

        private void OnRemoveGroup(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag;
            var group = (tag as ButtonGroup) ?? (_vm.Groups.Contains(_vm.SelectedGroup) ? _vm.SelectedGroup : null);
            if (group != null)
            {
                _vm.RemoveGroup(group);
            }
        }

        private void OnRemoveSelectedGroup(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.RemoveGroup(_vm.SelectedGroup);
            }
        }

        private void OnRemoveButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedButton != null && _vm.SelectedGroup != null)
            {
                _vm.RemoveButton(_vm.SelectedGroup, _vm.SelectedButton);
            }
        }

        private async void OnBrowseIcon(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedButton == null)
            {
                return;
            }

            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".ico");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _vm.SelectedButton.IconType = ToolbarIconType.Image;
                _vm.SelectedButton.IconPath = file.Path;
            }
        }
    }
}
