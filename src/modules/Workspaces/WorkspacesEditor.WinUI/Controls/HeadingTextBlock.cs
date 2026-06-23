// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace WorkspacesEditor.Controls
{
    /// <summary>
    /// A TextBlock-like control that reports itself as a Header for accessibility.
    /// WinUI TextBlock is sealed, so we wrap it in a custom control.
    /// </summary>
    public partial class HeadingTextBlock : Control
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(HeadingTextBlock), new PropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public HeadingTextBlock()
        {
            this.DefaultStyleKey = typeof(HeadingTextBlock);
            AutomationProperties.SetHeadingLevel(this, AutomationHeadingLevel.Level1);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new HeadingTextBlockAutomationPeer(this);
        }

        internal sealed partial class HeadingTextBlockAutomationPeer : FrameworkElementAutomationPeer
        {
            public HeadingTextBlockAutomationPeer(HeadingTextBlock owner)
                : base(owner)
            {
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Header;
            }

            protected override string GetNameCore()
            {
                return ((HeadingTextBlock)Owner).Text ?? string.Empty;
            }
        }
    }
}
