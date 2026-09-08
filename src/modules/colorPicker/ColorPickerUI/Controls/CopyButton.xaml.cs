// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace ColorPicker.Controls
{
    public sealed partial class CopyButton : Button
    {
        private const string CopiedToClipboardActivityId = "CopiedToClipboardActivityId";

        public static readonly DependencyProperty CopiedMessageProperty =
            DependencyProperty.Register(nameof(CopiedMessage), typeof(string), typeof(CopyButton), new PropertyMetadata(string.Empty));

        public CopyButton()
        {
            DefaultStyleKey = typeof(CopyButton);
        }

        public string CopiedMessage
        {
            get => (string)GetValue(CopiedMessageProperty);
            set => SetValue(CopiedMessageProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            Click -= CopyButton_Click;
            base.OnApplyTemplate();
            Click += CopyButton_Click;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetTemplateChild("CopyToClipboardSuccessAnimation") is Storyboard storyboard)
            {
                storyboard.Begin();
            }

            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                CopiedMessage,
                CopiedToClipboardActivityId);
        }
    }
}
