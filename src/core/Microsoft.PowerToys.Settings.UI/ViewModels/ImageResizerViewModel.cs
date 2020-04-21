// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ImageResizerViewModel : Observable
    {
        private ImageResizerSettings Settings { get; set; }

        private const string ModuleName = "ImageResizer";

        public ImageResizerViewModel()
        {
            try
            {
                Settings = SettingsUtils.GetSettings<ImageResizerSettings>(ModuleName);
            }
            catch
            {
                Settings = new ImageResizerSettings();
                SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
            }

            GeneralSettings generalSettings;

            try
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            catch
            {
                generalSettings = new GeneralSettings();
                SettingsUtils.SaveSettings(generalSettings.ToJsonString(), string.Empty);
            }

            this._isEnabled = generalSettings.Enabled.ImageResizer;
            this._advancedSizes = Settings.Properties.ImageresizerSizes.Value;

            Sizes.CollectionChanged += OnSizesCollectionChanged;
        }

        private bool _isEnabled = false;
        private ObservableCollection<ImageSize> _advancedSizes = new ObservableCollection<ImageSize>();

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettings generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                    generalSettings.Enabled.ImageResizer = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(generalSettings);
                    ShellPage.DefaultSndMSGCallback(snd.ToString());
                    OnPropertyChanged("IsEnabled");
                }
            }
        }

        public ObservableCollection<ImageSize> Sizes
        {
            get
            {
                return _advancedSizes;
            }

            set
            {
                _advancedSizes = value;
                Settings.Properties.ImageresizerSizes.Value = value;
            }
        }

        public ICommand DeleteImageSizeEventHandler
        {
            get
            {
                return new RelayCommand<int>(DeleteImageSize);
            }
        }

        public ICommand AddImageSizeEventHandler
        {
            get
            {
                return new RelayCommand(AddRow);
            }
        }

        public void AddRow()
        {
            Sizes.Add(new ImageSize());
        }

        public void DeleteImageSize(int id)
        {
            try
            {
                ImageSize size = Sizes.Where<ImageSize>(x => x.Id == id).First();
                Sizes.Remove(size);
            }
            catch
            {
            }
        }

        public void OnSizesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("Sizes");
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
        }
    }
}
