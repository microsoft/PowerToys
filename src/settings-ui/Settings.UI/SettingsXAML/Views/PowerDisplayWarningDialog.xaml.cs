// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Shared confirmation dialog for every Power Display action that can damage hardware
    /// or leave a monitor in a non-recoverable state. The caller picks a
    /// <see cref="PowerDisplayWarningKind"/> and the dialog renders a warning InfoBar
    /// (title from <c>PowerDisplay_Warning_{Kind}_InfoBar</c>), a body paragraph with
    /// bullets (from <c>PowerDisplay_Warning_{Kind}_Body</c>), and a shared
    /// title / learn-more hyperlink / Enable + Cancel buttons.
    /// </summary>
    public sealed partial class PowerDisplayWarningDialog : ContentDialog
    {
        // Shared across every variant; not localized.
        private const string LearnMoreUrl = "https://aka.ms/powerToysOverview_PowerDisplay_Note";

        public PowerDisplayWarningDialog(PowerDisplayWarningKind kind)
        {
            InitializeComponent();

            var loader = ResourceLoaderInstance.ResourceLoader;

            // Shared chrome: same title, hyperlink, and buttons on every variant.
            Title = loader.GetString("PowerDisplay_Warning_Title");
            PrimaryButtonText = loader.GetString("PowerDisplay_Dialog_Enable");
            CloseButtonText = loader.GetString("PowerDisplay_Dialog_Cancel");
            LearnMoreLink.Content = loader.GetString("PowerDisplay_Warning_LearnMore");
            LearnMoreLink.NavigateUri = new Uri(LearnMoreUrl);

            // Variant-specific content. The resw key pair is derived from the enum name so
            // adding a new warning is one enum value + two resw entries — no code change here.
            var prefix = $"PowerDisplay_Warning_{kind}";
            WarningInfoBar.Title = loader.GetString($"{prefix}_InfoBar");
            WarningBody.Text = loader.GetString($"{prefix}_Body");
        }
    }
}
