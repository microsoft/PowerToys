// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    // Lets the user pick a program out of the ones that are making sound. The list can be searched, because there can be a lot of them.
    public sealed partial class SoundManagerProgramPicker : UserControl
    {
        private readonly ResourceLoader _resourceLoader = ResourceLoaderInstance.ResourceLoader;

        private readonly ObservableCollection<SoundManagerProgram> _programs = new();

        private readonly ObservableCollection<SoundManagerProgram> _filteredPrograms = new();

        public SoundManagerProgramPicker()
        {
            InitializeComponent();
        }

        public event EventHandler<SoundManagerProgram> ProgramPicked;

        // Raised when the picked program is no longer on the list, so that the caller can take the button that adds it away again.
        public event EventHandler SelectionCleared;

        public ObservableCollection<SoundManagerProgram> FilteredPrograms => _filteredPrograms;

        public SoundManagerProgram SelectedProgram { get; private set; }

        public void SetPrograms(IEnumerable<SoundManagerProgram> programs)
        {
            _programs.Clear();

            foreach (var program in programs)
            {
                _programs.Add(program);
            }

            SearchBox.Text = string.Empty;
            ApplyFilter(string.Empty);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ApplyFilter(sender.Text);
        }

        private void ProgramList_ItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedProgram = e.ClickedItem as SoundManagerProgram;

            if (SelectedProgram != null)
            {
                ProgramPicked?.Invoke(this, SelectedProgram);
            }
        }

        private void ApplyFilter(string search)
        {
            _filteredPrograms.Clear();

            foreach (var program in _programs)
            {
                if (string.IsNullOrWhiteSpace(search) ||
                    program.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    program.Program.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                {
                    _filteredPrograms.Add(program);
                }
            }

            // A program that is not on the list any more cannot be added, so the choice is taken back.
            if (SelectedProgram != null && !_filteredPrograms.Contains(SelectedProgram))
            {
                SelectedProgram = null;
                SelectionCleared?.Invoke(this, EventArgs.Empty);
            }

            ShowEmptyState(search);
        }

        // The list says why it is empty, so that an empty list is not a puzzle.
        private void ShowEmptyState(string search)
        {
            if (_filteredPrograms.Count > 0)
            {
                EmptyStateText.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyStateText.Text = _resourceLoader.GetString(
                string.IsNullOrWhiteSpace(search)
                    ? "SoundManager_ProgramPicker_NothingPlaying/Text"
                    : "SoundManager_ProgramPicker_NoMatch/Text");
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }
}
