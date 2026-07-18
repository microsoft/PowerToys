// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerAccentReferenceGuidePage : NavigablePage
    {
        public PowerAccentReferenceGuideViewModel ViewModel { get; private set; }

        public PowerAccentReferenceGuidePage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Reinitialises the ViewModel each time the page is navigated to, so that any
        /// change to the user's language selection since the last visit is reflected.
        /// The navigation parameter should be the set of selected language code strings
        /// passed from <see cref="PowerAccentPage"/>.
        /// </summary>
        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var selectedCodes = e.Parameter as string[] ?? [];

            ViewModel = new PowerAccentReferenceGuideViewModel(
                selectedCodes,
                ResourceLoaderInstance.ResourceLoader.GetString);

            // CollectionViewSource is a resource and cannot track ViewModel changes via
            // x:Bind, so set its Source manually after the ViewModel is ready.
            LanguagesViewSource.Source = ViewModel.FilteredGroups;
            ViewModel.FilteredGroupsReplaced += (_, _) =>
            {
                // Swap to an empty collection first then back to the real one — WinUI 3's
                // CollectionViewSource does not observe grouped ObservableCollection mutations
                // directly, so we must reassign. Using an empty collection rather than null
                // avoids the crash that occurs when the ListView holds a live view reference.
                var groups = ViewModel.FilteredGroups;
                LanguagesViewSource.Source = new System.Collections.ObjectModel.ObservableCollection<ReferenceGroupModel>();
                LanguagesViewSource.Source = groups;

                Bindings.Update();
            };

            // ViewModel was null during InitializeComponent, so push all x:Bind values now.
            Bindings.Update();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void CharacterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: CharacterModel characterModel })
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(characterModel.Value);
                Clipboard.SetContent(dataPackage);
            }
        }
    }
}
