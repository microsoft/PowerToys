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
    /// Interaction logic for MainView.xaml
    /// </summary>
    public sealed partial class MainView : UserControl
    {
        private INotifyPropertyChanged _subscribedViewModel;

        public MainView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Subscribe(DataContext);
            UpdateAccessibleNames();
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
                UpdateAccessibleNames();
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
                UpdateAccessibleNames();
            }
        }

        // Sets AutomationProperties.Name on both live-region targets without raising
        // LiveRegionChanged, so screen readers are not triggered by silent initialisation
        // or settings-toggle events.
        private void UpdateAccessibleNames()
        {
            if (DataContext is not IMainViewModel vm)
            {
                // Explicitly clear stale names when no valid VM is present.
                AutomationProperties.SetName(ColorTextBlock, string.Empty);
                AutomationProperties.SetName(ColorTextWithNameBlock, string.Empty);
                return;
            }

            var colorText = vm.ColorText ?? string.Empty;
            var colorName = vm.ColorName ?? string.Empty;
            var twoLineName = string.IsNullOrEmpty(colorName) ? colorText : $"{colorText} {colorName}";

            AutomationProperties.SetName(ColorTextBlock, colorText);
            AutomationProperties.SetName(ColorTextWithNameBlock, twoLineName);
        }

        // Updates accessible names on both targets and raises LiveRegionChanged only on
        // the currently visible target so screen readers announce the new color.
        private void AnnounceCurrentColor()
        {
            if (DataContext is not IMainViewModel vm)
            {
                return;
            }

            var colorText = vm.ColorText ?? string.Empty;
            var colorName = vm.ColorName ?? string.Empty;
            var twoLineName = string.IsNullOrEmpty(colorName) ? colorText : $"{colorText} {colorName}";

            AutomationProperties.SetName(ColorTextBlock, colorText);
            AutomationProperties.SetName(ColorTextWithNameBlock, twoLineName);

            // Announce on the visible target only.
            FrameworkElement target = vm.ShowColorName ? ColorTextWithNameBlock : ColorTextBlock;

            var peer = FrameworkElementAutomationPeer.FromElement(target)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(target);

            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
