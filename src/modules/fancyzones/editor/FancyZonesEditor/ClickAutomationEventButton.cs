// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace FancyZonesEditor
{
    /// <summary>
    /// A <see cref="Button"/> that raises a UI Automation value-changed event when clicked,
    /// so a screen reader announces <see cref="OnClickAutomationValue"/> as the outcome of
    /// the click (used by the canvas editor to announce a newly added zone).
    /// </summary>
    public partial class ClickAutomationEventButton : Button
    {
        public static readonly DependencyProperty OnClickAutomationValueProperty =
            DependencyProperty.Register(
                nameof(OnClickAutomationValue),
                typeof(string),
                typeof(ClickAutomationEventButton),
                new PropertyMetadata(null));

        public ClickAutomationEventButton()
        {
            // Reuse the built-in Button template: a derived control has no implicit style
            // of its own in WinUI 3 and would otherwise render without a template.
            DefaultStyleKey = typeof(Button);
            Click += OnClick;
        }

        public string OnClickAutomationValue
        {
            get { return (string)GetValue(OnClickAutomationValueProperty); }
            set { SetValue(OnClickAutomationValueProperty, value); }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ClickAutomationEventButtonAutomationPeer(this);
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
            {
                return;
            }

            if (FrameworkElementAutomationPeer.FromElement(this) is ClickAutomationEventButtonAutomationPeer peer)
            {
                peer.RaisePropertyChangedEvent(
                    ValuePatternIdentifiers.ValueProperty,
                    null,
                    OnClickAutomationValue);
            }
        }

        public partial class ClickAutomationEventButtonAutomationPeer : ButtonAutomationPeer, IValueProvider
        {
            public ClickAutomationEventButtonAutomationPeer(ClickAutomationEventButton control)
                : base(control)
            {
            }

            public string Value
            {
                get { return MyOwner.OnClickAutomationValue; }
            }

            public bool IsReadOnly
            {
                get { return !IsEnabled(); }
            }

            private ClickAutomationEventButton MyOwner
            {
                get { return (ClickAutomationEventButton)Owner; }
            }

            public void SetValue(string value)
            {
                MyOwner.OnClickAutomationValue = value;
            }

            protected override string GetClassNameCore()
            {
                return nameof(ClickAutomationEventButton);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Button;
            }

            protected override object GetPatternCore(PatternInterface patternInterface)
            {
                if (patternInterface == PatternInterface.Value)
                {
                    return this;
                }

                return base.GetPatternCore(patternInterface);
            }
        }
    }
}
