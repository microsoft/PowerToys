// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A mode ComboBox (Default, Custom, Off, Wall) and, for Custom, its slider, as an edge row and
    /// a direction row of the CursorBrake edge editor show them.
    /// </summary>
    public abstract partial class CursorBrakeEdgeChoiceViewModel : Observable
    {
        private readonly CursorBrakeReselect _reselect = new CursorBrakeReselect();
        private int _selectedIndex;
        private int _pushDistance;
        private bool _defaultBrakes = true;
        private bool _applying;

        public IReadOnlyList<string> Items { get; private set; } = Array.Empty<string>();

        // Each item's text, for the ComboBoxItems the page declares. Declared items keep their
        // containers, so the ComboBox's UIA value is the selected text even before its list opens.
        // An item whose list has opened names itself from the text it drew there. A closed list does
        // not redraw that text. Default is the only item whose text changes, so the page binds its
        // automation name to DefaultText.
        public string DefaultText => ItemText(ItemOf(CursorBrakeEdgeMode.Default));

        public string CustomText => ItemText(ItemOf(CursorBrakeEdgeMode.Custom));

        public string OffText => ItemText(ItemOf(CursorBrakeEdgeMode.Off));

        public string WallText => ItemText(ItemOf(CursorBrakeEdgeMode.Wall));

        public int SelectedIndex
        {
            get => _reselect.IsActive ? -1 : _selectedIndex;
            set
            {
                if (_reselect.IsActive)
                {
                    return;
                }

                // A ComboBox clears its selection when its items are replaced, as when a list reuses
                // the row's container; that is never a choice, so it writes nothing, and the box is
                // told the value again so it does not stay empty.
                if (value < 0)
                {
                    OnPropertyChanged(nameof(SelectedIndex));
                    return;
                }

                if (Set(ref _selectedIndex, value))
                {
                    OnPropertyChanged(nameof(IsCustom));
                    OnPropertyChanged(nameof(ShownMode));
                    if (!_applying)
                    {
                        OnSelected(value);
                    }
                }
            }
        }

        public int PushDistance
        {
            get => _pushDistance;
            set
            {
                if (Set(ref _pushDistance, value) && IsCustom && !_applying)
                {
                    OnPushDistanceEdited();
                }
            }
        }

        public bool IsCustom => _selectedIndex == ItemOf(CursorBrakeEdgeMode.Custom);

        /// <summary>Gets the mode of the selected item, Default for an item that is no mode.</summary>
        public CursorBrakeEdgeMode Mode => ModeOf(_selectedIndex);

        /// <summary>
        /// Gets the mode the map draws: the selected one, with Default as Off where Apply to leaves
        /// the edge out, as the module treats it.
        /// </summary>
        public CursorBrakeEdgeMode ShownMode => Mode == CursorBrakeEdgeMode.Default && !_defaultBrakes ? CursorBrakeEdgeMode.Off : Mode;

        /// <summary>
        /// The ComboBox position of a mode: Default first, then the modes in their order. A value
        /// the enum does not define has Default's position.
        /// </summary>
        public static int ItemOf(CursorBrakeEdgeMode mode)
        {
            return (int)(Enum.IsDefined(mode) ? mode : CursorBrakeEdgeMode.Default) - (int)CursorBrakeEdgeMode.Default;
        }

        /// <summary>The mode at a ComboBox position, Default for a position that holds no mode.</summary>
        public static CursorBrakeEdgeMode ModeOf(int item)
        {
            var mode = (CursorBrakeEdgeMode)(item + (int)CursorBrakeEdgeMode.Default);
            return Enum.IsDefined(mode) ? mode : CursorBrakeEdgeMode.Default;
        }

        /// <summary>
        /// Shows values read from the settings without writing them back. <paramref name="defaultBrakes"/>
        /// says whether Apply to brakes the edge, which decides what Default does on it.
        /// </summary>
        internal void ShowChoice(int selectedIndex, int pushDistance, IReadOnlyList<string> items, bool defaultBrakes)
        {
            _applying = true;
            if (_defaultBrakes != defaultBrakes)
            {
                _defaultBrakes = defaultBrakes;
                OnPropertyChanged(nameof(ShownMode));
            }

            // Replacing the items clears the ComboBox's selection, so the same items stay, and new
            // items are followed by the value again.
            bool newItems = !Items.SequenceEqual(items);
            if (newItems)
            {
                Items = items;
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(DefaultText));
                OnPropertyChanged(nameof(CustomText));
                OnPropertyChanged(nameof(OffText));
                OnPropertyChanged(nameof(WallText));
                OnItemsChanged();
            }

            SelectedIndex = selectedIndex;
            if (newItems)
            {
                _reselect.Run(() => OnPropertyChanged(nameof(SelectedIndex)));
            }

            PushDistance = pushDistance;
            _applying = false;
        }

        protected string ItemText(int item) => item < Items.Count ? Items[item] : string.Empty;

        /// <summary>Called when the user picks an item.</summary>
        protected abstract void OnSelected(int item);

        /// <summary>Called when the user moves the slider while Custom is selected.</summary>
        protected abstract void OnPushDistanceEdited();

        /// <summary>Called after <see cref="Items"/> changes, for the text of items a subclass adds.</summary>
        protected virtual void OnItemsChanged()
        {
        }
    }
}
