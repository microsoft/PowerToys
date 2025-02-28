// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
                // Title = "Value preview",
                Title = name,
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

            // panel.Children.Add(nameBox);
            contentDialog.Content = panel;

            // Add content based on value type
            switch (type)
            {
                case "REG_DWORD":
                case "REG_QWORD":
                    var hexBox = new TextBox()
                    {
                        Header = "Value (hex)",
                        IsReadOnly = true,
                        Text = value.Split(" ")[0],
                    };
                    var dezBox = new TextBox()
                    {
                        Header = "Value (dec)",
                        IsReadOnly = true,
                        Text = value.Split(" ")[1].TrimStart('(').TrimEnd(')'),
                    };
                    panel.Children.Add(hexBox);
                    panel.Children.Add(dezBox);
                    break;
                case "REG_BINARY":
                case "REG_MULTI_SZ":
                    var multiLineBox = new TextBox()
                    {
                        Header = "Value",
                        IsReadOnly = true,
                        Text = value,
                        AcceptsReturn = true,
                    };
                    panel.Children.Add(multiLineBox);
                    break;
                case "REG_EXPAND_SZ":
                    var stringBoxRaw = new TextBox()
                    {
                        Header = "Value",
                        IsReadOnly = true,
                        Text = value,
                    };
                    var stringBoxExp = new TextBox()
                    {
                        Header = "Value (expanded)",
                        IsReadOnly = true,
                        Text = Environment.ExpandEnvironmentVariables(value),
                    };
                    panel.Children.Add(stringBoxRaw);
                    panel.Children.Add(stringBoxExp);
                    break;
                default: // REG_SZ
                    var stringBox = new TextBox()
                    {
                        Header = "Value",
                        IsReadOnly = true,
                        Text = value,
                    };
                    panel.Children.Add(stringBox);
                    break;
            }

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
