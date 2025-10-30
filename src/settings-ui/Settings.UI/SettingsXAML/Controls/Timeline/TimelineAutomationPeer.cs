// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class TimelineAutomationPeer : FrameworkElementAutomationPeer
    {
        public TimelineAutomationPeer(Timeline owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => "Timeline";

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.Custom;

        protected override string GetAutomationIdCore()
        {
            var owner = (Timeline)Owner;
            var id = AutomationProperties.GetAutomationId(owner);
            return string.IsNullOrEmpty(id) ? base.GetAutomationIdCore() : id;
        }

        protected override string GetNameCore()
        {
            var owner = (Timeline)Owner;
            var name = AutomationProperties.GetName(owner);
            return !string.IsNullOrEmpty(name)
                ? name
                : $"Timeline from {owner.StartTime} to {owner.EndTime}";
        }
    }
}
