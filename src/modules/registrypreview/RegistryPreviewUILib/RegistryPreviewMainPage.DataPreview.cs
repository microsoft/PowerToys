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
                Content = $"{name} - {type} - {value}",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.None,
            };

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
