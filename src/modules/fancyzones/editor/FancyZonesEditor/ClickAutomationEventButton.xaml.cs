// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace FancyZonesEditor
{
    public partial class ClickAutomationEventButton : Button
    {
        public ClickAutomationEventButton()
            : base()
        {
            InitializeComponent();
            Click += OnClick;
        }

        public string OnClickAutomationValue
        {
            get { return (string)GetValue(OnClickAutomationValueProperty); }
            set { SetValue(OnClickAutomationValueProperty, value); }
        }

        public static readonly DependencyProperty OnClickAutomationValueProperty =
        DependencyProperty.Register(
            "Value", typeof(string), typeof(ClickAutomationEventButton));

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                if (UIElementAutomationPeer.FromElement(this) is ClickAutomationEventButtonAutomationPeer peer)
                {
                    peer.RaisePropertyChangedEvent(
                        ValuePatternIdentifiers.ValueProperty,
                        null,
                        OnClickAutomationValue);
                }
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ClickAutomationEventButtonAutomationPeer(this);
        }

        public class ClickAutomationEventButtonAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
        {
            public ClickAutomationEventButtonAutomationPeer(ClickAutomationEventButton control)
                : base(control)
            {
            }

            protected override string GetClassNameCore()
            {
                return nameof(ClickAutomationEventButton);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Button;
            }

            public override object GetPattern(PatternInterface patternInterface)
            {
                if (patternInterface == PatternInterface.Value)
                {
                    return this;
                }

                return base.GetPattern(patternInterface);
            }

            public void SetValue(string value)
            {
                MyOwner.OnClickAutomationValue = value;
            }

            private ClickAutomationEventButton MyOwner
            {
                get
                {
                    return (ClickAutomationEventButton)Owner;
                }
            }

            public string Value
            {
                get { return MyOwner.OnClickAutomationValue; }
            }

            public bool IsReadOnly
            {
                get { return !IsEnabled(); }
            }
        }
    }
}
