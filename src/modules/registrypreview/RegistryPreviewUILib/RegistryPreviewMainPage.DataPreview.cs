// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using HB = RegistryPreviewUILib.HexBox;

namespace RegistryPreviewUILib
{
    public sealed partial class RegistryPreviewMainPage : Page
    {
        private static bool _isDataPreviewHexBoxLoaded;

        internal void ShowEnhancedDataPreview(string name, string type, string value)
        {
            // Get resources
            var xamlPageResources = this.Resources;

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
                    // For more printable asci characters the following code lines are required:
                    //  var cpW1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252);
                    //  || b == 128 || (b >= 130 && b <= 140) || b == 142 || (b >= 145 & b <= 156) || b >= 158
                    //  cpW1252.GetString([b]);
                    string binaryDataText = string.Empty;
                    foreach (byte b in byteArray)
                    {
                        // ASCII codes:
                        //  9, 10, 13: Space, Line Feed, Carriage Return
                        //  32-126: Printable characters
                        //  128, 130-140, 142, 145-156, 158-255: Extended printable characters
                        if (b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126))
                        {
                            binaryDataText += Convert.ToChar(b);
                        }
                    }

                    // Add controls
                    AddBinaryView(ref panel, ref resourceLoader, ref binaryData, binaryDataText, ref xamlPageResources);
                    break;
                case "REG_MULTI_SZ":
                    var multiLineBox = new TextBox()
                    {
                        IsReadOnly = true,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.NoWrap,
                        MaxHeight = 200,
                        FontSize = 14,
                        RequestedTheme = panel.ActualTheme,
                        Text = value,
                    };
                    ScrollViewer.SetVerticalScrollBarVisibility(multiLineBox, ScrollBarVisibility.Auto);
                    ScrollViewer.SetHorizontalScrollBarVisibility(multiLineBox, ScrollBarVisibility.Auto);
                    AutomationProperties.SetName(multiLineBox, resourceLoader.GetString("DataPreview_AutomationPropertiesName_MultilineTextValue"));
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
                        RequestedTheme = panel.ActualTheme,
                        Text = value,
                    };
                    AutomationProperties.SetName(stringBox, resourceLoader.GetString("DataPreview_AutomationPropertiesName_TextValue"));
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
                RequestedTheme = panel.ActualTheme,
                Text = value.Split(" ")[0],
            };
            var decimalBox = new TextBox()
            {
                Header = resourceLoader.GetString("DataPreviewDec"),
                IsReadOnly = true,
                FontSize = 14,
                RequestedTheme = panel.ActualTheme,
                Text = value.Split(" ")[1].TrimStart('(').TrimEnd(')'),
            };
            panel.Children.Add(hexBox);
            panel.Children.Add(decimalBox);
        }

        private static void AddBinaryView(ref StackPanel panel, ref ResourceLoader resourceLoader, ref BinaryReader data, string dataText, ref ResourceDictionary xamlPageResources)
        {
            // Create SelectorBar
            var navBar = new SelectorBar()
            {
                RequestedTheme = panel.ActualTheme,
            };
            navBar.SelectionChanged += BinaryPreview_SelectorChanged;
            navBar.Items.Add(new SelectorBarItem()
            {
                Text = resourceLoader.GetString("DataPreviewDataView"),
                Tag = "DataView",
                FontSize = 14,
                RequestedTheme = panel.ActualTheme,
                IsSelected = true,
            });
            navBar.Items.Add(new SelectorBarItem()
            {
                Text = resourceLoader.GetString("DataPreviewVisibleText"),
                Tag = "TextView",
                FontSize = 14,
                RequestedTheme = panel.ActualTheme,
                IsSelected = false,
                IsEnabled = !string.IsNullOrWhiteSpace(dataText),
            });

            // Create HexBox
            var binaryPreviewBox = new HB.HexBox()
            {
                Height = 300,
                Width = 495,
                ShowAddress = true,
                ShowData = true,
                ShowText = true,
                Columns = 8,
                FontSize = 13,
                RequestedTheme = panel.ActualTheme,
                AddressBrush = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                AlternatingDataColumnTextBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                SelectionTextBrush = (Brush)xamlPageResources["HexBox_SelectionTextBrush"],
                SelectionBrush = (Brush)xamlPageResources["HexBox_SelectionBackgroundBrush"],
                DataFormat = HB.DataFormat.Hexadecimal,
                DataSignedness = HB.DataSignedness.Unsigned,
                DataType = HB.DataType.Int_1,
                EnforceProperties = true,
                Visibility = Visibility.Collapsed,
                DataSource = data,
            };
            AutomationProperties.SetName(binaryPreviewBox, resourceLoader.GetString("DataPreview_AutomationPropertiesName_BinaryDataPreview"));
            binaryPreviewBox.Loaded += BinaryPreview_HexBoxLoaded;

            // Create TextBox
            var visibleText = new TextBox()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 300,
                Width = 495,
                FontSize = 13,
                Text = dataText,
                RequestedTheme = panel.ActualTheme,
                Visibility = Visibility.Collapsed,
            };
            AutomationProperties.SetName(visibleText, resourceLoader.GetString("DataPreview_AutomationPropertiesName_VisibleTextPreview"));

            // Add controls: 0 = SelectorBar, 1 = ProgressRing, 2 = HexBox, 3 = TextBox
            panel.Children.Add(navBar);
            panel.Children.Add(new ProgressRing());
            panel.Children.Add(binaryPreviewBox);
            panel.Children.Add(visibleText);
        }

        private static void AddExpandStringView(ref StackPanel panel, ref ResourceLoader resourceLoader, string value)
        {
            var stringBoxRaw = new TextBox()
            {
                Header = resourceLoader.GetString("DataPreviewRawValue"),
                IsReadOnly = true,
                FontSize = 14,
                RequestedTheme = panel.ActualTheme,
                Text = value,
            };
            var stringBoxExp = new TextBox()
            {
                Header = resourceLoader.GetString("DataPreviewExpandedValue"),
                IsReadOnly = true,
                FontSize = 14,
                RequestedTheme = panel.ActualTheme,
                Text = Environment.ExpandEnvironmentVariables(value),
            };
            panel.Children.Add(stringBoxRaw);
            panel.Children.Add(stringBoxExp);
        }

        private static void BinaryPreview_SelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            // Child controls: 0 = SelectorBar, 1 = ProgressRing, 2 = HexBox, 3 = TextBox
            var stackPanel = sender.Parent as StackPanel;
            var progressRing = (ProgressRing)stackPanel.Children[1];
            var hexBox = (HB.HexBox)stackPanel.Children[2];
            var textBox = (TextBox)stackPanel.Children[3];

            if (sender.SelectedItem.Tag.ToString() == "DataView")
            {
                textBox.Visibility = Visibility.Collapsed;
                if (_isDataPreviewHexBoxLoaded)
                {
                    progressRing.Visibility = Visibility.Collapsed;
                    hexBox.Visibility = Visibility.Visible;

                    // Clear selection aligned to TextBox
                    hexBox.ClearSelection();
                }
                else
                {
                    hexBox.Visibility = Visibility.Collapsed;
                    progressRing.Visibility = Visibility.Visible;
                }
            }
            else
            {
                progressRing.Visibility = Visibility.Collapsed;

                hexBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;

                // Workaround for wrong text selection (color) after switching back to "Visible text"
                textBox.Focus(FocusState.Programmatic);
                textBox.Select(0, 0);
            }
        }

        private static void BinaryPreview_HexBoxLoaded(object sender, RoutedEventArgs e)
        {
            _isDataPreviewHexBoxLoaded = true;
            var hexBox = (HB.HexBox)sender;
            var stackPanel = hexBox.Parent as StackPanel;
            var selectorBar = stackPanel.Children[0] as SelectorBar;
            var progressRing = stackPanel.Children[1] as ProgressRing;

            if (selectorBar.SelectedItem.Tag.ToString() == "DataView")
            {
                progressRing.Visibility = Visibility.Collapsed;
                hexBox.Visibility = Visibility.Visible;
            }
        }
    }
}
