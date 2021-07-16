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
    public partial class MyButton : Button
    {
        public MyButton()
            : base()
        {
            InitializeComponent();
            Click += OnClick;
        }

        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            "Value", typeof(string), typeof(MyButton));

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                MyButtonAutomationPeer peer =
                    UIElementAutomationPeer.FromElement(this) as MyButtonAutomationPeer;

                if (peer != null)
                {
                    peer.RaisePropertyChangedEvent(
                        ValuePatternIdentifiers.ValueProperty,
                        null,
                        Value);
                }
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MyButtonAutomationPeer(this);
        }

        public class MyButtonAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
        {
            public MyButtonAutomationPeer(MyButton control)
                : base(control)
            {
            }

            protected override string GetClassNameCore()
            {
                return "MyButton";
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
                MyOwner.Value = value;
            }

            private MyButton MyOwner
            {
                get
                {
                    return (MyButton)Owner;
                }
            }

            public string Value
            {
                get { return MyOwner.Value; }
            }

            public bool IsReadOnly
            {
                get { return !IsEnabled(); }
            }
        }
    }
}
