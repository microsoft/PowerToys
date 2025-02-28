// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Metadata;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        internal void ShowEnhancedDataPreview(string name, string type, string value)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                Title = "Value preview",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.None,
            };

            // Add default content
            var panel = new StackPanel()
            {
                Spacing = 16,
            };
            var nameBox = new TextBox()
            {
                Header = "Name",
                IsReadOnly = true,
                Text = name,
            };
            panel.Children.Add(nameBox);
            contentDialog.Content = panel;

            // Add content based on value type
            var stringBox = new TextBox()
            {
                Header = "Value",
                IsReadOnly = true,
                Text = value,
            };
            panel.Children.Add(stringBox);

            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                contentDialog.XamlRoot = this.Content.XamlRoot;
            }

            _ = contentDialog.ShowAsync();
        }
    }
}
