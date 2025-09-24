// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ReleaseNotePage : Page
    {
        public ReleaseNotePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ReleaseNotesItem item)
            {
                MarkdownBlock.Text = item.Markdown ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(item.HeaderImageUri) &&
                    Uri.TryCreate(item.HeaderImageUri, UriKind.Absolute, out var uri))
                {
                    HeaderImage.Source = new BitmapImage(uri);
                    HeaderImage.Visibility = Visibility.Visible;
                }
                else
                {
                    HeaderImage.Source = null;
                    HeaderImage.Visibility = Visibility.Collapsed;
                }
            }

            base.OnNavigatedTo(e);
        }
    }
}
