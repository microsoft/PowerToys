// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class TextExpanderPage : NavigablePage, IRefreshablePage, INotifyPropertyChanged
    {
        private int? _editingId;
        private string _snippetProblem;
        private string _snippetPreview;
        private bool _isSnippetValid;

        public TextExpanderPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new TextExpanderViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, PickSingleFolderDialog);
            DataContext = ViewModel;
            this.InitializeComponent();
            BuildVariableMenu();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Opens the folder browser for the snippet folder.
        ///
        /// <para>
        /// Uses the shell32 dialog rather than <c>PickSingleFolderAsync</c> for the same reason
        /// the General page does: the WinRT picker does not work when the process is elevated,
        /// and Settings can be.
        /// </para>
        /// </summary>
        private async Task<string> PickSingleFolderDialog()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            return await Task.FromResult<string>(ShellGetFolder.GetFolderDialog(hwnd));
        }

        /// <summary>Gets the live preview of the replacement being edited.</summary>
        public string SnippetPreview
        {
            get => _snippetPreview;
            private set => Set(ref _snippetPreview, value);
        }

        /// <summary>Gets the reason the snippet cannot be saved, or null.</summary>
        public string SnippetProblem
        {
            get => _snippetProblem;
            private set
            {
                if (Set(ref _snippetProblem, value))
                {
                    OnPropertyChanged(nameof(HasSnippetProblem));
                }
            }
        }

        public bool HasSnippetProblem => !string.IsNullOrEmpty(_snippetProblem);

        /// <summary>Gets a value indicating whether Save is allowed.</summary>
        public bool IsSnippetValid
        {
            get => _isSnippetValid;
            private set => Set(ref _isSnippetValid, value);
        }

        private TextExpanderViewModel ViewModel { get; set; }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        /// <summary>
        /// Builds the Insert variable menu from the view model, so the menu cannot drift from the
        /// set of variables the engine can actually expand.
        /// </summary>
        private void BuildVariableMenu()
        {
            foreach (TextExpanderVariable variable in ViewModel.Variables)
            {
                var item = new MenuFlyoutItem
                {
                    Text = variable.HasSample
                        ? variable.Name + "  \u2014  " + variable.Sample
                        : variable.Name,
                    Tag = variable.Token,
                };

                item.Click += InsertVariable_Click;
                VariableFlyout.Items.Add(item);
            }
        }

        private void InsertVariable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.Tag is not string token)
            {
                return;
            }

            // Insert at the caret rather than appending, so a variable can go in the middle of a
            // sentence -- which is where most of them belong.
            int start = ReplacementBox.SelectionStart;
            string text = ReplacementBox.Text ?? string.Empty;
            int afterSelection = start + ReplacementBox.SelectionLength;

            ReplacementBox.Text = string.Concat(
                text.AsSpan(0, start), token, text.AsSpan(afterSelection));
            ReplacementBox.SelectionStart = start + token.Length;
            ReplacementBox.SelectionLength = 0;
            ReplacementBox.Focus(FocusState.Programmatic);
        }

        private async void AddSnippet_Click(object sender, RoutedEventArgs e)
        {
            await ShowSnippetDialogAsync(null, string.Empty, string.Empty);
        }

        private async void EditSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: TextExpanderSnippetViewModel snippet })
            {
                await ShowSnippetDialogAsync(snippet.Id, snippet.Trigger, snippet.Replacement);
            }
        }

        private async void DuplicateSnippet_Click(object sender, RoutedEventArgs e)
        {
            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            if (snippet == null)
            {
                return;
            }

            string error = ViewModel.DuplicateSnippet(snippet.Id);
            if (error != null)
            {
                await ShowErrorAsync(error);
            }
        }

        private async void DeleteSnippet_Click(object sender, RoutedEventArgs e)
        {
            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            if (snippet == null)
            {
                return;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // Confirm, because there is no undo and the file is the only copy.
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_Title"),
                Content = string.Format(
                    CultureInfo.CurrentCulture,
                    resourceLoader.GetString("TextExpander_DeleteSnippetDialog_Content"),
                    snippet.Trigger),
                PrimaryButtonText = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_PrimaryButtonText"),
                CloseButtonText = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_CloseButtonText"),
                DefaultButton = ContentDialogButton.Close,
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            string error = ViewModel.DeleteSnippet(snippet.Id);
            if (error != null)
            {
                await ShowErrorAsync(error);
            }
        }

        private static TextExpanderSnippetViewModel GetSnippetFromSender(object sender)
        {
            if (sender is FrameworkElement { DataContext: TextExpanderSnippetViewModel dataContextSnippet })
            {
                return dataContextSnippet;
            }

            return sender is FrameworkElement { Tag: TextExpanderSnippetViewModel tagSnippet } ? tagSnippet : null;
        }

        private async System.Threading.Tasks.Task ShowSnippetDialogAsync(int? id, string trigger, string replacement)
        {
            _editingId = id;
            TriggerBox.Text = trigger ?? string.Empty;
            ReplacementBox.Text = replacement ?? string.Empty;

            SnippetDialog.XamlRoot = XamlRoot;
            Validate();

            // Loop so a refused save reopens with the text intact. Closing and making the user
            // retype because a trigger was already taken would be a hostile way to say so.
            while (await SnippetDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string error = ViewModel.SaveSnippet(_editingId, TriggerBox.Text, ReplacementBox.Text);
                if (error == null)
                {
                    return;
                }

                SnippetProblem = error;
                IsSnippetValid = false;
            }
        }

        private void SnippetField_Changed(object sender, TextChangedEventArgs e) => Validate();

        private void Validate()
        {
            string trigger = TriggerBox.Text ?? string.Empty;
            string replacement = ReplacementBox.Text ?? string.Empty;

            SnippetPreview = TextExpanderViewModel.PreviewReplacement(replacement);

            if (!TextExpanderSnippetFile.IsValidTrigger(trigger, out string problem))
            {
                // An empty trigger is the starting state, not a mistake worth shouting about.
                SnippetProblem = trigger.Length == 0 ? null : problem;
                IsSnippetValid = false;
                return;
            }

            SnippetProblem = null;
            IsSnippetValid = true;
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString("TextExpander_SaveSnippetsErrorDialog_Title"),
                Content = message,
                CloseButtonText = resourceLoader.GetString("TextExpander_SaveSnippetsErrorDialog_CloseButtonText"),
            };

            await dialog.ShowAsync();
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
