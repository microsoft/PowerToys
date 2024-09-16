// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Settings;
using AdvancedPaste.ViewModels;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Controls
{
    public sealed partial class PromptBox : Microsoft.UI.Xaml.Controls.UserControl
    {
        // Minimum time to show spinner when generating custom format using forcePasteCustom
        private static readonly TimeSpan MinTaskTime = TimeSpan.FromSeconds(2);

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly IUserSettings _userSettings;

        public OptionsViewModel ViewModel { get; private set; }

        public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: string.Empty));

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
            nameof(Footer),
            typeof(object),
            typeof(PromptBox),
            new PropertyMetadata(defaultValue: null));

        public object Footer
        {
            get => (object)GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public PromptBox()
        {
            this.InitializeComponent();

            _userSettings = App.GetService<IUserSettings>();

            ViewModel = App.GetService<OptionsViewModel>();
            ViewModel.CustomActionActivated += (_, e) => GenerateCustom(e.ForcePasteCustom);
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            InputTxtBox.Focus(FocusState.Programmatic);
        }

        [RelayCommand]
        private void GenerateCustom() => GenerateCustom(false);

        private void GenerateCustom(bool forcePasteCustom)
        {
            Logger.LogTrace();

            VisualStateManager.GoToState(this, "LoadingState", true);
            string inputInstructions = ViewModel.Query;
            ViewModel.SaveQuery(inputInstructions);

            var customFormatTask = ViewModel.GenerateCustomFunction(inputInstructions);
            var delayTask = forcePasteCustom ? Task.Delay(MinTaskTime) : Task.CompletedTask;
            Task.WhenAll(customFormatTask, delayTask)
                .ContinueWith(
                _ =>
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        ViewModel.CustomFormatResult = customFormatTask.Result;

                        if (ViewModel.ApiRequestStatus == (int)HttpStatusCode.OK)
                        {
                            VisualStateManager.GoToState(this, "DefaultState", true);
                            if (_userSettings.ShowCustomPreview && !forcePasteCustom)
                            {
                                PreviewGrid.Width = InputTxtBox.ActualWidth;
                                PreviewFlyout.ShowAt(InputTxtBox);
                            }
                            else
                            {
                                ViewModel.PasteCustom();
                                InputTxtBox.Text = string.Empty;
                            }
                        }
                        else
                        {
                            VisualStateManager.GoToState(this, "ErrorState", true);
                        }
                    });
                },
                TaskScheduler.Default);
        }

        [RelayCommand]
        private void Recall()
        {
            Logger.LogTrace();

            InputTxtBox.IsEnabled = true;

            var lastQuery = ViewModel.RecallPreviousCustomQuery();
            if (lastQuery != null)
            {
                InputTxtBox.Text = lastQuery.Query;
            }

            ClipboardHelper.SetClipboardTextContent(lastQuery.ClipboardData);
        }

        private void InputTxtBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && InputTxtBox.Text.Length > 0 && ViewModel.IsCustomAIEnabled)
            {
                GenerateCustom();
            }
        }

        private void PreviewPasteBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PasteCustom();
            InputTxtBox.Text = string.Empty;
        }

        private void ThumbUpDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                bool result;
                if (bool.TryParse(btn.CommandParameter as string, out result))
                {
                    PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteCustomFormatOutputThumbUpDownEvent(result));
                }
            }
        }

        private void PreviewFlyout_Opened(object sender, object e)
        {
            PreviewPasteBtn.Focus(FocusState.Programmatic);
        }

        internal void IsLoading(bool loading)
        {
            Loader.IsLoading = loading;
        }
    }
}
