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
    /// resource key prefix; the dialog loads
    /// "{prefix}_WarningTitle/Header/Description/WarningList_Item1/2/3/Confirm".
    /// Bullets are prepended in code so translators only see the body text.
    /// </summary>
    public sealed partial class DangerousFeatureWarningDialog : ContentDialog
    {
        private const string BulletPrefix = "• ";

        public DangerousFeatureWarningDialog(string resourceKeyPrefix)
        {
            InitializeComponent();

            var loader = ResourceLoaderInstance.ResourceLoader;
            Title = loader.GetString($"{resourceKeyPrefix}_WarningTitle");
            WarningHeader.Text = loader.GetString($"{resourceKeyPrefix}_WarningHeader");
            WarningDescription.Text = loader.GetString($"{resourceKeyPrefix}_WarningDescription");
            WarningListItem1.Text = BulletPrefix + loader.GetString($"{resourceKeyPrefix}_WarningList_Item1");
            WarningListItem2.Text = BulletPrefix + loader.GetString($"{resourceKeyPrefix}_WarningList_Item2");
            WarningListItem3.Text = BulletPrefix + loader.GetString($"{resourceKeyPrefix}_WarningList_Item3");
            WarningConfirm.Text = loader.GetString($"{resourceKeyPrefix}_WarningConfirm");
            PrimaryButtonText = loader.GetString("PowerDisplay_Dialog_Enable");
            CloseButtonText = loader.GetString("PowerDisplay_Dialog_Cancel");
        }
    }
}
