// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class ShortcutControlAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, IValueProvider
    {
        public ShortcutControlAutomationPeer(ShortcutControl owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(ShortcutControl);

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.Button;

        protected override string GetNameCore()
        {
            var owner = (ShortcutControl)Owner;
            var name = owner.GetAutomationName();

            return string.IsNullOrWhiteSpace(name)
                ? base.GetNameCore()
                : name;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.Invoke => this,
                PatternInterface.Value => this,
                _ => base.GetPattern(patternInterface),
            };
        }

        public void Invoke()
        {
            var owner = (ShortcutControl)Owner;
            if (!owner.IsShortcutEnabled())
            {
                throw new InvalidOperationException("Cannot invoke shortcut control: the control is currently disabled.");
            }

            owner.Invoke();
        }

        public string Value => ((ShortcutControl)Owner).GetAutomationValue();

        public bool IsReadOnly => true;

        public void SetValue(string value)
        {
            throw new InvalidOperationException("Cannot modify shortcut value: the shortcut control value is read-only. Use Invoke to open the shortcut configuration dialog.");
        }
    }
}
