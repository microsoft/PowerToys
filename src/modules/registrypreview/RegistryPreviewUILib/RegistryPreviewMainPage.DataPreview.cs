// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HexBox;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation.Metadata;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        internal void ShowEnhancedDataPreview(string name, string type, string value)
        {
            // Format value based on type
            if (type == "REG_NONE" || type == "REG_BINARY")
            {
                value = string.Join("\r", Regex.Matches(value, ".{0,24}").Select(x => x.Value.ToUpper(System.Globalization.CultureInfo.CurrentCulture).Trim().Replace(" ", "\t")));
            }

            // Create dialog
            var panel = new StackPanel()
            {
                Spacing = 16,
            };
            ContentDialog contentDialog = new ContentDialog()
            {
                // Title = "Value preview",
                Title = "View data - " + name,
                Content = panel,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.None,
            };

            // Add content based on value type
            switch (type)
            {
                case "REG_DWORD":
                case "REG_QWORD":
                    var hexBox = new TextBox()
                    {
                        Header = "Hexadecimal",
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value.Split(" ")[0],
                    };
                    var decimalBox = new TextBox()
                    {
                        Header = "Decimal",
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value.Split(" ")[1].TrimStart('(').TrimEnd(')'),
                    };
                    panel.Children.Add(hexBox);
                    panel.Children.Add(decimalBox);
                    break;
                case "REG_NONE":
                case "REG_BINARY":
                    /* // Convert data
                    byte[] byteArray = Encoding.UTF8.GetBytes(value);
                    MemoryStream memoryStream = new MemoryStream(byteArray);
                    BinaryReader binaryData = new BinaryReader(memoryStream);
                    binaryData.ReadBytes(byteArray.Length);

                    // Create box
                    var binaryBox = new HexBox.WinUI.HexBox()
                    {
                        Height = 200,
                        Width = 500,
                        ShowAddress = true,
                        ShowData = true,
                        ShowText = true,
                        Columns = 8,
                        DataFormat = HexBox.WinUI.DataFormat.Hexadecimal,
                        DataSignedness = HexBox.WinUI.DataSignedness.Unsigned,
                        DataType = HexBox.WinUI.DataType.Int_2,
                        DataSource = binaryData,
                        Visibility = Visibility.Visible,
                    };
                    panel.Children.Add(binaryBox);
                    break; */
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
                        Header = "Raw value",
                        IsReadOnly = true,
                        FontSize = 14,
                        Text = value,
                    };
                    var stringBoxExp = new TextBox()
                    {
                        Header = "Expanded value",
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
    }
}
