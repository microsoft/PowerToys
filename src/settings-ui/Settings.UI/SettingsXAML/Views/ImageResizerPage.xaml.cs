// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Windows.Input;

using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ImageResizerPage : NavigablePage, IRefreshablePage, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ImageResizerViewModel ViewModel { get; set; }

        public ICommand AddCommand => new RelayCommand(Add);

        public ICommand UpdateCommand => new RelayCommand(Update);

        private readonly ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;

        // Working copy shown in the edit dialog, bound via x:Bind. Edits happen on this copy so a
        // cancel simply discards it; for edits it is a clone of the original, for adds a new model.
        private ImageSize _editingSize = new ImageSize();

        // The original preset being edited, or null when adding a new one.
        private ImageSize _editOriginal;

        public ImageSize EditingSize
        {
            get => _editingSize;
            set
            {
                _editingSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditingSize)));
            }
        }

        // Header for the dimensions field. When Height isn't used (e.g. percentage scaling that keeps
        // the aspect ratio) the single value scales the whole image, so "Width" would be misleading.
        public string GetDimensionHeader(bool isHeightUsed) =>
            resourceLoader.GetString(isHeightUsed ? "ImageResizer_Dimensions_Width" : "ImageResizer_Dimensions_Size");

        public ImageResizerPage()
        {
            InitializeComponent();
            var settingsUtils = SettingsUtils.Default;
            Func<string, string> loader = resourceLoader.GetString;

            ViewModel = new ImageResizerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, loader);
            DataContext = ViewModel;
        }

        public async void DeleteCustomSize(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuItem || menuItem.CommandParameter is not ImageSize size)
            {
                return;
            }

            ContentDialog dialog = new ContentDialog();
            dialog.XamlRoot = RootPage.XamlRoot;
            dialog.Title = size.Name;
            dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
            dialog.CloseButtonText = resourceLoader.GetString("No");
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") };
            dialog.PrimaryButtonClick += (s, args) =>
            {
                ViewModel.DeleteImageSize(size.Id);
            };
            await dialog.ShowAsync();
        }

        private async void AddSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _editOriginal = null;
                EditingSize = ViewModel.CreateNewImageSizeModel();
                EditSizeDialog.Title = resourceLoader.GetString("ImageResizer_EditSizeDialog_AddTitle");
                EditSizeDialog.PrimaryButtonText = resourceLoader.GetString("ImageResizer_EditSizeDialog_Save");
                EditSizeDialog.PrimaryButtonCommand = AddCommand;
                await EditSizeDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception encountered when opening the add image size dialog.", ex);
            }
        }

        private async void EditSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not SettingsCard card || card.DataContext is not ImageSize original)
            {
                return;
            }

            try
            {
                // Edit a working copy so changes can be discarded on cancel without touching the original.
                _editOriginal = original;
                EditingSize = original.Clone();
                EditSizeDialog.Title = resourceLoader.GetString("ImageResizer_EditSizeDialog_EditTitle");
                EditSizeDialog.PrimaryButtonText = resourceLoader.GetString("ImageResizer_EditSizeDialog_Update");
                EditSizeDialog.PrimaryButtonCommand = UpdateCommand;
                await EditSizeDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception encountered when opening the edit image size dialog.", ex);
            }
        }

        private void Add()
        {
            ViewModel.AddImageSize(EditingSize);
            EditSizeDialog.Hide();
        }

        private void Update()
        {
            if (_editOriginal != null)
            {
                ViewModel.UpdateImageSize(_editOriginal, EditingSize);
            }

            EditSizeDialog.Hide();
        }

        private void ImagesSizesListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (ViewModel.IsListViewFocusRequested)
            {
                // Set focus to the last item in the ListView
                int size = ImagesSizesListView.Items.Count;
                ((ListViewItem)ImagesSizesListView.ContainerFromIndex(size - 1)).Focus(FocusState.Programmatic);

                // Reset the focus requested flag
                ViewModel.IsListViewFocusRequested = false;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
