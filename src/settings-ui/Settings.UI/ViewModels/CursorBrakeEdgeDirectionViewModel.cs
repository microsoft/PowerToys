// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// One direction of an edge set to "Different each way": its ComboBox (Default, Custom, Off,
    /// Wall) and, for Custom, its slider.
    /// </summary>
    public partial class CursorBrakeEdgeDirectionViewModel : CursorBrakeEdgeChoiceViewModel
    {
        private readonly Action _edited;

        public CursorBrakeEdgeDirectionViewModel(int from, int to, IReadOnlyList<CursorBrakeTextPart> headerParts, string automationId, Action edited)
        {
            From = from;
            To = to;
            HeaderParts = headerParts;
            Header = string.Concat(headerParts.Select(p => p.Plain));
            AutomationId = automationId;
            _edited = edited;
        }

        /// <summary>Gets the index of the display this direction leaves.</summary>
        public int From { get; }

        /// <summary>Gets the index of the display this direction enters.</summary>
        public int To { get; }

        /// <summary>Gets the header as text, with each monitor's number written out.</summary>
        public string Header { get; }

        /// <summary>Gets the header as shown: each monitor as its numbered icon and its name.</summary>
        public IReadOnlyList<CursorBrakeTextPart> HeaderParts { get; }

        /// <summary>Gets the automation id of the direction's combo box; its slider's adds "_Distance".</summary>
        public string AutomationId { get; }

        public string SliderAutomationId => AutomationId + "_Distance";

        protected override void OnSelected(int item)
        {
            _edited();
        }

        protected override void OnPushDistanceEdited()
        {
            _edited();
        }
    }
}
