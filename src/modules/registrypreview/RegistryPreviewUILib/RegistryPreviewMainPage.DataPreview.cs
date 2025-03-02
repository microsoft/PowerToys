// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Unicode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Foundation.Metadata;
using HB = HexBox.WinUI;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        private static bool _isDataPreviewHexBoxLoaded;

        internal void ShowEnhancedDataPreview(string name, string type, string value)
        {
            // Create dialog
            _isDataPreviewHexBoxLoaded = false;
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
                    AddHexView(ref panel, ref resourceLoader, value);
                    break;
                case "REG_NONE":
                case "REG_BINARY":
                    // Convert value to BinaryReader
                    byte[] byteArray = Convert.FromHexString(value.Replace(" ", string.Empty));
                    MemoryStream memoryStream = new MemoryStream(byteArray);
                    BinaryReader binaryData = new BinaryReader(memoryStream);
                    binaryData.ReadBytes(byteArray.Length);

                    // Convert value to text
                    // For more printable asci characters the following code lines are reuqired:
                    //  var cpW1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252);
                    //  || b == 128 || (b >= 130 && b <= 140) || b == 142 || (b >= 145 & b <= 156) || b >= 158
                    //  cpW1252.GetString([b]);
                    string binaryDataText = string.Empty;
                    foreach (byte b in byteArray)
                    {
                        // ASCII codes:
                        //  9, 10, 13: Space, Line Feed, Carriage Return
                        //  32-126: Printable characters
                        //  128, 130-140, 142, 145-156, 158-255: Extendet printable characters
                        if (b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126))
                        {
                            binaryDataText += Convert.ToChar(b);
                        }
                    }

                    // Add controls
                    AddBinaryView(ref panel, ref resourceLoader, ref binaryData, binaryDataText);
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
                    AddExpandStringView(ref panel, ref resourceLoader, value);
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

        private static void AddHexView(ref StackPanel panel, ref ResourceLoader resourceLoader, string value)
        {
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
        }

        private static void AddBinaryView(ref StackPanel panel, ref ResourceLoader resourceLoader, ref BinaryReader data, string dataText)
        {
            // Add SelectorBar
            var navBar = new SelectorBar();
            navBar.SelectionChanged += BinaryPreviewSelectorChanged;
            navBar.Items.Add(new SelectorBarItem()
            {
                Text = resourceLoader.GetString("DataPreviewDataView"),
                Tag = "DataView",
                FontSize = 14,
                IsSelected = true,
            });
            navBar.Items.Add(new SelectorBarItem()
            {
                Text = resourceLoader.GetString("DataPreviewVisibleText"),
                Tag = "TextView",
                FontSize = 14,
                IsSelected = false,
            });
            panel.Children.Add(navBar);

            // Add loading animation
            var ring = new ProgressRing();
            panel.Children.Add(ring);

            // Add hex box to dialog
            var binaryPreviewBox = new HB.HexBox()
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
                DataSource = data,
                Visibility = Visibility.Collapsed,
            };
            binaryPreviewBox.Loaded += BinaryPreviewLoaded;
            panel.Children.Add(binaryPreviewBox);

            // Add text box to dialog
            var txt = new TextBox()
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 300,
                Width = 500,
                FontSize = 13,
                Text = dataText,
                Visibility = Visibility.Collapsed,
            };
            panel.Children.Add(txt);
        }

        private static void AddExpandStringView(ref StackPanel panel, ref ResourceLoader resourceLoader, string value)
        {
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
        }

        private static void BinaryPreviewLoaded(object sender, RoutedEventArgs e)
        {
            _isDataPreviewHexBoxLoaded = true;

            var stackPanel = ((HB.HexBox)sender).Parent as StackPanel;
            var selectorBar = stackPanel.Children[0] as SelectorBar;

            // Item 0 is the "Data" item
            if (selectorBar.Items.IndexOf(selectorBar.SelectedItem) == 0)
            {
                // progress ring
                stackPanel.Children[1].Visibility = Visibility.Collapsed;

                // hex box
                ((HB.HexBox)sender).Visibility = Visibility.Visible;
            }
        }

        private static void BinaryPreviewSelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            // Child controls: 0 = SelectorBar, 1 = ProgressRing, 2 = HexBox, 3 = TextBox
            var stackPanel = ((SelectorBar)sender).Parent as StackPanel;

            if (sender.SelectedItem.Tag.ToString() == "DataView")
            {
                stackPanel.Children[3].Visibility = Visibility.Collapsed;
                if (_isDataPreviewHexBoxLoaded)
                {
                    stackPanel.Children[1].Visibility = Visibility.Collapsed;
                    stackPanel.Children[2].Visibility = Visibility.Visible;
                }
                else
                {
                    stackPanel.Children[1].Visibility = Visibility.Visible;
                    stackPanel.Children[2].Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                stackPanel.Children[1].Visibility = Visibility.Collapsed;
                stackPanel.Children[2].Visibility = Visibility.Collapsed;
                stackPanel.Children[3].Visibility = Visibility.Visible;
            }
        }
    }
}
