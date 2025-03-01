// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Metadata;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        internal void ShowEnhancedDataPreview(string name, string type, string value)
        {
            // Create dialog
            var panel = new StackPanel()
            {
                Spacing = 16,
                Padding = new Thickness(0),
            };
            ContentDialog contentDialog = new ContentDialog()
            {
                Title = resourceLoader.GetString("DataPreviewTitle") + " - " + name,
                Content = panel,
                CloseButtonText = resourceLoader.GetString("DataPreviewClose"),
                DefaultButton = ContentDialogButton.Primary,
                Padding = new Thickness(0),
            };

            // Add content based on value type
            switch (type)
            {
                case "REG_DWORD":
                case "REG_QWORD":
                    var hexBox = new TextBox()
                    {
                        Header = resourceLoader.GetString("DataPreviewHex"),
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value.Split(" ")[0],
                    };
                    var decimalBox = new TextBox()
                    {
                        Header = resourceLoader.GetString("DataPreviewDec"),
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value.Split(" ")[1].TrimStart('(').TrimEnd(')'),
                    };
                    panel.Children.Add(hexBox);
                    panel.Children.Add(decimalBox);
                    break;
                case "REG_NONE":
                case "REG_BINARY":
                    // Convert data
                    byte[] byteArray = Convert.FromHexString(value.Replace(" ", string.Empty));
                    MemoryStream memoryStream = new MemoryStream(byteArray);
                    BinaryReader binaryData = new BinaryReader(memoryStream);
                    binaryData.ReadBytes(byteArray.Length);

                    // Add loading animation
                    var ring = new ProgressRing();
                    panel.Children.Add(ring);

                    // Create hex box to dialog
                    var binaryPreviewBox = new HexBox.WinUI.HexBox()
                    {
                        Height = 300,
                        Width = 500,
                        ShowAddress = true,
                        ShowData = true,
                        ShowText = true,
                        Columns = 8,
                        FontSize = 13,
                        DataFormat = HexBox.WinUI.DataFormat.Hexadecimal,
                        DataSignedness = HexBox.WinUI.DataSignedness.Unsigned,
                        DataType = HexBox.WinUI.DataType.Int_1,
                        DataSource = binaryData,
                        Visibility = Visibility.Collapsed,
                    };
                    binaryPreviewBox.Loaded += BinaryPreviewLoaded;
                    panel.Children.Add(binaryPreviewBox);
                    break;
                case "REG_MULTI_SZ":
                    var multiLineBox = new TextBox()
                    {
                        IsReadOnly = true,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.NoWrap,
                        MaxHeight = 200,
                        FontSize = 14,
                        Text = value,
                    };
                    ScrollViewer.SetVerticalScrollBarVisibility(multiLineBox, ScrollBarVisibility.Auto);
                    ScrollViewer.SetHorizontalScrollBarVisibility(multiLineBox, ScrollBarVisibility.Auto);
                    panel.Children.Add(multiLineBox);
                    break;
                case "REG_EXPAND_SZ":
                    var stringBoxRaw = new TextBox()
                    {
                        Header = resourceLoader.GetString("DataPreviewRawValue"),
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value,
                    };
                    var stringBoxExp = new TextBox()
                    {
                        Header = resourceLoader.GetString("DataPreviewExpandedValue"),
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = Environment.ExpandEnvironmentVariables(value),
                    };
                    panel.Children.Add(stringBoxRaw);
                    panel.Children.Add(stringBoxExp);
                    break;
                default: // REG_SZ
                    var stringBox = new TextBox()
                    {
                        IsReadOnly = true,
                        FontSize = 14,
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

        private static void BinaryPreviewLoaded(object sender, RoutedEventArgs e)
        {
            // progress ring
            var p = ((HexBox.WinUI.HexBox)sender).Parent as StackPanel;
            p.Children[0].Visibility = Visibility.Collapsed;

            // hex box
            ((HexBox.WinUI.HexBox)sender).Visibility = Visibility.Visible;
        }
    }
}
