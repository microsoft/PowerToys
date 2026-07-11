// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using ColorPicker.ViewModelContracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for ColorPickerView.xaml
    /// </summary>
    public sealed partial class ColorPickerView : UserControl
    {
        private INotifyPropertyChanged _subscribedViewModel;

        public ColorPickerView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Subscribe(DataContext);
            UpdateAccessibleName();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unsubscribe();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Unsubscribe();
            if (IsLoaded)
            {
                Subscribe(args.NewValue);
                UpdateAccessibleName();
            }
        }

        private void Subscribe(object dataContext)
        {
            if (dataContext is not IMainViewModel || dataContext is not INotifyPropertyChanged vm)
            {
                return;
            }

            // Loaded may fire more than once; only subscribe once per VM instance.
            if (ReferenceEquals(_subscribedViewModel, vm))
            {
                return;
            }

            Unsubscribe();
            _subscribedViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel = null;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IMainViewModel.ColorName))
            {
                AnnounceCurrentColor();
            }
            else if (e.PropertyName == nameof(IMainViewModel.ShowColorName))
            {
                // Keep accessible names current when the setting changes; do not announce.
                UpdateAccessibleName();
            }
        }

        // Updates the live-region name without raising LiveRegionChanged, so screen readers are
        // not triggered by silent initialization or settings changes.
        private void UpdateAccessibleName()
        {
            if (DataContext is not IMainViewModel vm)
            {
                AutomationProperties.SetName(ColorTextBlock, string.Empty);
                return;
            }

            AutomationProperties.SetName(ColorTextBlock, GetAccessibleName(vm));
        }

        private void AnnounceCurrentColor()
        {
            if (DataContext is not IMainViewModel vm)
            {
                return;
            }

            AutomationProperties.SetName(ColorTextBlock, GetAccessibleName(vm));

            var peer = FrameworkElementAutomationPeer.FromElement(ColorTextBlock)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(ColorTextBlock);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        private static string GetAccessibleName(IMainViewModel vm)
        {
            var colorText = vm.ColorText ?? string.Empty;
            var colorName = vm.ColorName ?? string.Empty;

            return vm.ShowColorName && !string.IsNullOrEmpty(colorName)
                ? $"{colorText} {colorName}"
                : colorText;
        }
    }
}
