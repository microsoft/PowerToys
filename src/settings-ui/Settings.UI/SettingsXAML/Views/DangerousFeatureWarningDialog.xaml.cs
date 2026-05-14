// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Confirmation dialog shown when the user enables a feature that can damage the
    /// hardware or otherwise leave it in a non-recoverable state. The caller supplies a
    /// resource key prefix; the dialog loads "{prefix}_WarningTitle/Header/Description/List/Confirm".
    /// </summary>
    public sealed partial class DangerousFeatureWarningDialog : ContentDialog
    {
        public DangerousFeatureWarningDialog(string resourceKeyPrefix)
        {
            InitializeComponent();

            var loader = ResourceLoaderInstance.ResourceLoader;
            Title = loader.GetString($"{resourceKeyPrefix}_WarningTitle");
            WarningHeader.Text = loader.GetString($"{resourceKeyPrefix}_WarningHeader");
            WarningDescription.Text = loader.GetString($"{resourceKeyPrefix}_WarningDescription");
            WarningList.Text = loader.GetString($"{resourceKeyPrefix}_WarningList");
            WarningConfirm.Text = loader.GetString($"{resourceKeyPrefix}_WarningConfirm");
            PrimaryButtonText = loader.GetString("PowerDisplay_Dialog_Enable");
            CloseButtonText = loader.GetString("PowerDisplay_Dialog_Cancel");
        }
    }
}
