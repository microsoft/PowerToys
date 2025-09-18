// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class OOBEControl : UserControl
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
         nameof(Icon),
         typeof(ImageSource),
         typeof(OOBEControl),
         new PropertyMetadata(defaultValue: null));

        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
         nameof(Title),
         typeof(string),
         typeof(OOBEControl),
         new PropertyMetadata(defaultValue: null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
         nameof(Subtitle),
         typeof(string),
         typeof(OOBEControl),
         new PropertyMetadata(defaultValue: null));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly DependencyProperty NavigationItemsProperty = DependencyProperty.Register(
         nameof(NavigationItems),
         typeof(ObservableCollection<OOBEItem>),
         typeof(OOBEControl),
         new PropertyMetadata(defaultValue: new ObservableCollection<OOBEItem>()));

        public ObservableCollection<OOBEItem> NavigationItems
        {
            get => (ObservableCollection<OOBEItem>)GetValue(NavigationItemsProperty);
            set => SetValue(NavigationItemsProperty, value);
        }

        public OOBEControl()
        {
            InitializeComponent();
        }

        private void NavigationList_Loaded(object sender, RoutedEventArgs e)
        {
            navigationList.SelectedIndex = 0;
        }

        private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Assign DataTemplate for selected items
            foreach (var item in e.AddedItems)
            {
                if (sender is ListView lv && lv.ContainerFromItem(item) is ListViewItem lvi)
                {
                    lvi.ContentTemplate = (DataTemplate)this.Resources["SelectedNavigationTemplate"];
                }
            }

            // Remove DataTemplate for unselected items
            foreach (var item in e.RemovedItems)
            {
                if (sender is ListView lv && lv.ContainerFromItem(item) is ListViewItem lvi)
                {
                    lvi.ContentTemplate = (DataTemplate)this.Resources["DefaultNavigationTemplate"];
                }
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            int count = navigationList.SelectedIndex + 1;

            if (count >= navigationList.Items.Count)
            {
                count = 0;
            }

            navigationList.SelectedIndex = count;
        }

        private void FlipView_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
