// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Confirmation dialog shown when the user enables a feature that can damage the
    /// hardware or otherwise leave it in a non-recoverable state. The caller supplies a
    /// resource key prefix; the dialog loads
    /// "{prefix}_WarningTitle/Header/Description/WarningList_Item{N}/Confirm".
    /// Bullets are prepended in code so translators only see the body text; the
    /// item loop reads <c>_WarningList_Item1</c>, <c>_Item2</c>, ... until a missing
    /// key returns empty, so adding a 4th bullet only requires a new resw entry.
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
            WarningConfirm.Text = loader.GetString($"{resourceKeyPrefix}_WarningConfirm");
            PrimaryButtonText = loader.GetString("PowerDisplay_Dialog_Enable");
            CloseButtonText = loader.GetString("PowerDisplay_Dialog_Cancel");

            // ResourceLoader.GetString returns string.Empty for missing keys (see
            // FriendlyDateHelper.cs for the same pattern), so the loop stops cleanly
            // at the first absent _Item{N}.
            var items = new List<string>();
            for (int i = 1; ; i++)
            {
                var item = loader.GetString($"{resourceKeyPrefix}_WarningList_Item{i}");
                if (string.IsNullOrEmpty(item))
                {
                    break;
                }

                items.Add(BulletPrefix + item);
            }

            WarningList.ItemsSource = items;
        }
    }
}
