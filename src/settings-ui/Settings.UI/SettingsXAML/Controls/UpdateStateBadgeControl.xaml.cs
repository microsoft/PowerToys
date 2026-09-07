// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UpdateStateBadgeControl : UserControl
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                nameof(State),
                typeof(UpdateViewModel.UpdateUIState),
                typeof(UpdateStateBadgeControl),
                new PropertyMetadata(UpdateViewModel.UpdateUIState.UpToDate, OnStateChanged));

        private bool _isLoaded;

        public UpdateViewModel.UpdateUIState State
        {
            get => (UpdateViewModel.UpdateUIState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public UpdateStateBadgeControl()
        {
            InitializeComponent();
            Loaded += UpdateStateBadgeControl_Loaded;
        }

        private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var control = (UpdateStateBadgeControl)dependencyObject;
            if (control._isLoaded)
            {
                control.UpdateVisualState();
            }
        }

        private void UpdateStateBadgeControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            string stateName = State switch
            {
                UpdateViewModel.UpdateUIState.Checking => "CheckingState",
                UpdateViewModel.UpdateUIState.ReadyToDownload or
                UpdateViewModel.UpdateUIState.ReadyToInstall => "AttentionState",
                UpdateViewModel.UpdateUIState.Downloading => "DownloadingState",
                UpdateViewModel.UpdateUIState.NetworkError or
                UpdateViewModel.UpdateUIState.ErrorDownloading => "ErrorState",
                _ => "SuccessState",
            };

            VisualStateManager.GoToState(this, stateName, false);
        }
    }
}
