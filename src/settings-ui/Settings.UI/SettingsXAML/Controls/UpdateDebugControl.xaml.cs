// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UpdateDebugControl : UserControl
    {
#if DEBUG
        private static readonly UpdateViewModel.UpdateUIState[] DebugPreviewStates =
        {
            UpdateViewModel.UpdateUIState.UpToDate,
            UpdateViewModel.UpdateUIState.Checking,
            UpdateViewModel.UpdateUIState.NetworkError,
            UpdateViewModel.UpdateUIState.ReadyToDownload,
            UpdateViewModel.UpdateUIState.Downloading,
            UpdateViewModel.UpdateUIState.ReadyToInstall,
            UpdateViewModel.UpdateUIState.ErrorDownloading,
        };

        private static readonly UpdateViewModel.UpdateUIState[] DebugFlowStates =
        {
            UpdateViewModel.UpdateUIState.Checking,
            UpdateViewModel.UpdateUIState.NetworkError,
            UpdateViewModel.UpdateUIState.Checking,
            UpdateViewModel.UpdateUIState.ReadyToDownload,
            UpdateViewModel.UpdateUIState.Downloading,
            UpdateViewModel.UpdateUIState.ErrorDownloading,
            UpdateViewModel.UpdateUIState.Downloading,
            UpdateViewModel.UpdateUIState.ReadyToInstall,
            UpdateViewModel.UpdateUIState.UpToDate,
        };

        private readonly DispatcherQueueTimer _debugFlowTimer;
        private int _debugFlowIndex;
        private bool _isChangingDebugSelection;
#endif

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(UpdateViewModel),
                typeof(UpdateDebugControl),
                new PropertyMetadata(null));

        public UpdateViewModel ViewModel
        {
            get => (UpdateViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public UpdateDebugControl()
        {
            InitializeComponent();
#if DEBUG
            Visibility = Visibility.Visible;
            _debugFlowTimer = DispatcherQueue.CreateTimer();
            _debugFlowTimer.Interval = TimeSpan.FromSeconds(1.5);
            _debugFlowTimer.Tick += DebugFlowTimer_Tick;
            Loaded += UpdateDebugControl_Loaded;
            _isChangingDebugSelection = true;
            DebugPreviewStateComboBox.SelectedIndex = 0;
            _isChangingDebugSelection = false;
            Unloaded += UpdateDebugControl_Unloaded;
#endif
        }

#if DEBUG
        private void UpdateDebugControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SynchronizeDebugSelection();
        }

        private void UpdateDebugControl_Unloaded(object sender, RoutedEventArgs e)
        {
            StopDebugFlow();
            if (ViewModel is not null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(UpdateViewModel.CurrentUpdateUIState) or nameof(UpdateViewModel.IsPreviewing))
            {
                SynchronizeDebugSelection();
            }
        }

        private void SynchronizeDebugSelection()
        {
            _isChangingDebugSelection = true;
            DebugPreviewStateComboBox.SelectedIndex = ViewModel is null || !ViewModel.IsPreviewing
                ? 0
                : Array.IndexOf(DebugPreviewStates, ViewModel.CurrentUpdateUIState) + 1;
            _isChangingDebugSelection = false;
        }
#endif

        private void DebugPreviewStateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
#if DEBUG
            if (_isChangingDebugSelection)
            {
                return;
            }

            StopDebugFlow();
            int stateIndex = DebugPreviewStateComboBox.SelectedIndex - 1;
            ViewModel?.SetDebugPreviewState(stateIndex >= 0 ? DebugPreviewStates[stateIndex] : null);
#endif
        }

        private void DebugRunFlowButton_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            StopDebugFlow();
            _debugFlowIndex = 0;
            DebugRunFlowButton.IsEnabled = false;
            ShowNextDebugFlowState();
            if (_debugFlowIndex < DebugFlowStates.Length)
            {
                _debugFlowTimer.Start();
            }
#endif
        }

#if DEBUG
        private void DebugFlowTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            ShowNextDebugFlowState();
        }

        private void ShowNextDebugFlowState()
        {
            SetDebugPreviewState(DebugFlowStates[_debugFlowIndex]);
            _debugFlowIndex++;

            if (_debugFlowIndex >= DebugFlowStates.Length)
            {
                StopDebugFlow();
            }
        }

        private void SetDebugPreviewState(UpdateViewModel.UpdateUIState state)
        {
            ViewModel?.SetDebugPreviewState(state);
            _isChangingDebugSelection = true;
            DebugPreviewStateComboBox.SelectedIndex = Array.IndexOf(DebugPreviewStates, state) + 1;
            _isChangingDebugSelection = false;
        }

        private void StopDebugFlow()
        {
            _debugFlowTimer.Stop();
            DebugRunFlowButton.IsEnabled = true;
        }
#endif
    }
}
