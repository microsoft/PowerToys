// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class VcpValueBlockEditorDialog : ContentDialog
    {
        public VcpValueBlockEditorDialog(MonitorInfo monitor)
        {
            ViewModel = new VcpValueBlockEditorViewModel(monitor);
            InitializeComponent();

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_DisabledOptionsEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Save");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel");
        }

        public VcpValueBlockEditorViewModel ViewModel { get; }

        public List<VcpValueBlock>? ResultBlocks { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ResultBlocks = ViewModel.CreateValueBlocks();
        }
    }
}
