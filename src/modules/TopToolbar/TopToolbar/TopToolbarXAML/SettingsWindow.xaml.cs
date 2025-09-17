// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TopToolbar.Models;
using TopToolbar.Services;
using TopToolbar.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TopToolbar
{
    public sealed partial class SettingsWindow : WinUIEx.WindowEx, IDisposable
    {
        private readonly SettingsViewModel _vm;

        public SettingsViewModel ViewModel => _vm;

        public SettingsWindow()
        {
            this.InitializeComponent();
            _vm = new SettingsViewModel(new ToolbarConfigService());
            this.Closed += async (s, e) =>
            {
                await _vm.SaveAsync();
            };
            this.Activated += async (s, e) =>
            {
                if (_vm.Groups.Count == 0)
                {
                    await _vm.LoadAsync(this.DispatcherQueue);
                }
            };

            // Keep left pane visible when no selection so UI doesn't look empty
            _vm.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedGroup) ||
                e.PropertyName == nameof(SettingsViewModel.HasNoSelectedGroup))
            {
                var leftCol = LeftPaneColumn;
                if (leftCol != null && _vm.HasNoSelectedGroup)
                {
                    leftCol.Width = new GridLength(240);
                }
            }
        }

        private void OnToggleGroupsPane(object sender, RoutedEventArgs e)
        {
            var leftCol = LeftPaneColumn;
            if (leftCol != null)
            {
                leftCol.Width = (leftCol.Width.Value == 0) ? new GridLength(240) : new GridLength(0);
            }
        }

        private async void OnAddGroup(object sender, RoutedEventArgs e)
        {
            _vm.AddGroup();
            await _vm.SaveAsync();
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            await _vm.SaveAsync();
        }

        private async void OnClose(object sender, RoutedEventArgs e)
        {
            await _vm.SaveAsync();
            this.Close();
        }

        private async void OnAddButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.AddButton(_vm.SelectedGroup);
                await _vm.SaveAsync();
            }
        }

        private async void OnRemoveGroup(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag;
            var group = (tag as ButtonGroup) ?? (_vm.Groups.Contains(_vm.SelectedGroup) ? _vm.SelectedGroup : null);
            if (group != null)
            {
                _vm.RemoveGroup(group);
                await _vm.SaveAsync();
            }
        }

        private async void OnRemoveSelectedGroup(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.RemoveGroup(_vm.SelectedGroup);
                await _vm.SaveAsync();
            }
        }

        private async void OnRemoveButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedButton != null && _vm.SelectedGroup != null)
            {
                _vm.RemoveButton(_vm.SelectedGroup, _vm.SelectedButton);
                await _vm.SaveAsync();
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
                await _vm.SaveAsync();
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
