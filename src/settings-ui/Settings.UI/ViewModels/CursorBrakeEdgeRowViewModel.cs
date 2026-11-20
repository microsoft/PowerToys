// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// One edge between two displays in the CursorBrake edge editor: its ComboBox, its slider for
    /// Custom, and its two direction rows when it is set to "Different each way".
    /// </summary>
    public partial class CursorBrakeEdgeRowViewModel : CursorBrakeEdgeChoiceViewModel
    {
        // The position in Items after the four modes, which a direction row does not have.
        public const int DifferentEachWayItem = 4;

        private readonly Action<CursorBrakeEdgeRowViewModel> _edited;
        private readonly Action<CursorBrakeEdgeRowViewModel> _selected;
        private bool _isSelected;
        private bool _isSplit;
        private IReadOnlyList<CursorBrakeTextPart> _descriptionParts = Array.Empty<CursorBrakeTextPart>();

        public CursorBrakeEdgeRowViewModel(int edgeIndex, int first, int second, IReadOnlyList<CursorBrakeTextPart> titleParts, bool isEditable, CursorBrakeEdgeDirectionViewModel forward, CursorBrakeEdgeDirectionViewModel backward, Action<CursorBrakeEdgeRowViewModel> edited, Action<CursorBrakeEdgeRowViewModel> selected)
        {
            EdgeIndex = edgeIndex;
            First = first;
            Second = second;
            TitleParts = titleParts;
            Title = string.Concat(titleParts.Select(p => p.Plain));
            IsEditable = isEditable;
            Forward = forward;
            Backward = backward;
            _edited = edited;
            _selected = selected;
        }

        /// <summary>Gets the index of the edge in the module's detected edges.</summary>
        public int EdgeIndex { get; }

        /// <summary>Gets the letter that names the edge on the map and in the list: A for the first edge.</summary>
        public string Letter => LetterOf(EdgeIndex);

        /// <summary>Gets the index of the first display, the one <see cref="Forward"/> leaves.</summary>
        public int First { get; }

        public int Second { get; }

        /// <summary>Gets the title as text, with each monitor's number written out.</summary>
        public string Title { get; }

        /// <summary>Gets the title as shown: each monitor as its numbered icon and its name.</summary>
        public IReadOnlyList<CursorBrakeTextPart> TitleParts { get; }

        /// <summary>
        /// Gets a value indicating whether the edge can carry settings: both displays have an ID.
        /// </summary>
        public bool IsEditable { get; }

        public CursorBrakeEdgeDirectionViewModel Forward { get; }

        public CursorBrakeEdgeDirectionViewModel Backward { get; }

        public string DifferentEachWayText => ItemText(DifferentEachWayItem);

        /// <summary>Gets the description as text, with each monitor's number written out.</summary>
        public string Description => string.Concat(_descriptionParts.Select(p => p.Plain));

        /// <summary>Gets the description as shown, with the wall note's monitors as icons.</summary>
        public IReadOnlyList<CursorBrakeTextPart> DescriptionParts
        {
            get => _descriptionParts;
            internal set
            {
                if (Set(ref _descriptionParts, value))
                {
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public bool IsSplit
        {
            get => _isSplit;
            private set => Set(ref _isSplit, value);
        }

        public string AutomationName => $"{Letter}: {Title}";

        // Automation ids by letter, for the UI tests and scripted runs.
        public string ModeAutomationId => "MouseUtils_CursorBrakeEdgeMode_" + Letter;

        public string SliderAutomationId => "MouseUtils_CursorBrakeEdgeDistance_" + Letter;

        /// <summary>Gets a value indicating whether the edge is the one selected on the map.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            internal set => Set(ref _isSelected, value);
        }

        /// <summary>The letter of the edge at <paramref name="edgeIndex"/>: A to Z, then numbers.</summary>
        public static string LetterOf(int edgeIndex)
        {
            return edgeIndex < 26 ? ((char)('A' + edgeIndex)).ToString() : (edgeIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Selects the edge on the map, as when its row takes focus.</summary>
        public void Select()
        {
            _selected(this);
        }

        /// <summary>Shows values read from the settings without writing them back.</summary>
        internal void Show(int selectedIndex, int pushDistance, bool isSplit, IReadOnlyList<string> items, bool defaultBrakes)
        {
            ShowChoice(selectedIndex, pushDistance, items, defaultBrakes);
            IsSplit = isSplit;
        }

        protected override void OnSelected(int item)
        {
            IsSplit = item == DifferentEachWayItem;
            _edited(this);
        }

        protected override void OnPushDistanceEdited()
        {
            _edited(this);
        }

        protected override void OnItemsChanged()
        {
            OnPropertyChanged(nameof(DifferentEachWayText));
        }
    }
}
