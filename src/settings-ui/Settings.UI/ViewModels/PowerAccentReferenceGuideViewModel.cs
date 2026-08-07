// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerAccent.Common;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerAccentReferenceGuideViewModel : Observable
    {
        private string _searchQuery = string.Empty;

        /// <summary>
        /// Raised once after <see cref="FilteredGroups"/> has been fully mutated by a
        /// filter change. The view subscribes to this instead of <see cref="ObservableCollection{T}.CollectionChanged"/>
        /// so that the <c>CollectionViewSource</c> source-swap happens exactly once per
        /// filter operation rather than once per item mutation.
        /// </summary>
        public event EventHandler FilteredGroupsReplaced;

        /// <summary>
        /// Gets or sets the current search query. Setting this filters
        /// <see cref="FilteredGroups"/> in real time.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    UpdateFilteredGroups(value);
                    OnPropertyChanged(nameof(SearchQuery));
                    OnPropertyChanged(nameof(IsEmpty));
                    FilteredGroupsReplaced?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// The full unfiltered reference data, built once at construction time from
        /// <see cref="CharacterMappings.All"/> using the provided selected language codes.
        /// </summary>
        private readonly IReadOnlyList<ReferenceGroupModel> _allGroups;

        /// <summary>
        /// Gets the reference data filtered by <see cref="SearchQuery"/>. When the query
        /// is empty the full list is returned. When non-empty, only languages that contain
        /// the query string as a substring of any mapped character are included.
        /// Matching is case-insensitive. No Unicode normalisation is performed.
        /// </summary>
        /// <remarks>
        /// This is an <see cref="ObservableCollection{T}"/> that is mutated in place on
        /// each filter change so that the bound <c>ListView</c> only updates changed
        /// items rather than rebuilding from scratch.
        /// </remarks>
        public ObservableCollection<ReferenceGroupModel> FilteredGroups { get; } = new();

        /// <summary>
        /// Gets a value indicating whether <see cref="FilteredGroups"/> is empty, i.e.
        /// the search query produced no matches. Used to show an empty-state message in
        /// the UI.
        /// </summary>
        public bool IsEmpty => FilteredGroups.Count == 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerAccentReferenceGuideViewModel"/> class.
        /// </summary>
        /// <param name="selectedLanguageCodes">
        /// The set of language codes (i.e. <see cref="Language"/> enum names) currently
        /// selected by the user. Selected languages are surfaced at the top of each
        /// group and visually distinguished from unselected ones.
        /// </param>
        /// <param name="getLocalizedString">
        /// Delegate used to resolve localised strings from resource IDs. Injected to
        /// keep the ViewModel testable without a live resource loader.
        /// </param>
        public PowerAccentReferenceGuideViewModel(
            IReadOnlyCollection<string> selectedLanguageCodes,
            Func<string, string> getLocalizedString)
        {
            _allGroups = BuildGroups(selectedLanguageCodes, getLocalizedString);
            foreach (var group in _allGroups)
            {
                FilteredGroups.Add(group);
            }
        }

        private void UpdateFilteredGroups(string query)
        {
            var matching = string.IsNullOrEmpty(query)
                ? _allGroups
                : _allGroups
                    .Select(group =>
                    {
                        var langs = group.Languages
                            .Where(lang => lang.KeyMappings
                                .Any(mapping => mapping.Characters
                                    .Any(ch => ch.Value.Contains(query, StringComparison.OrdinalIgnoreCase))))
                            .ToList();
                        return langs.Count > 0
                            ? new ReferenceGroupModel { GroupHeader = group.GroupHeader, Languages = langs }
                            : null;
                    })
                    .Where(g => g != null)
                    .OfType<ReferenceGroupModel>()
                    .ToList();

            // Mutate the existing collection in place: remove groups no longer present,
            // add new ones. This preserves the ListView's item containers and avoids a
            // full re-render of the visible list.
            for (int i = FilteredGroups.Count - 1; i >= 0; i--)
            {
                if (!matching.Any(g => g.GroupHeader == FilteredGroups[i].GroupHeader))
                {
                    FilteredGroups.RemoveAt(i);
                }
            }

            for (int i = 0; i < matching.Count; i++)
            {
                if (i >= FilteredGroups.Count || FilteredGroups[i].GroupHeader != matching[i].GroupHeader)
                {
                    FilteredGroups.Insert(i, matching[i]);
                }
                else
                {
                    // Group is present - replace it so language membership updates.
                    FilteredGroups[i] = matching[i];
                }
            }
        }

        private static IReadOnlyList<ReferenceGroupModel> BuildGroups(
            IReadOnlyCollection<string> selectedLanguageCodes,
            Func<string, string> getLocalizedString)
        {
            // Build the flat list of reference models from CharacterMappings.All,
            // resolving display names via the resource loader.
            var allModels = CharacterMappings.All
                .Select(lang =>
                {
                    bool isSelected = selectedLanguageCodes.Contains(lang.Id.ToString(), StringComparer.OrdinalIgnoreCase);
                    string displayName = getLocalizedString($"QuickAccent_SelectedLanguage_{lang.Identifier}");

                    var keyMappings = lang.Characters
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => new KeyMappingModel(
                            kvp.Key.ToString()
                                .Replace("VK_", string.Empty, StringComparison.Ordinal)
                                .TrimEnd('_'),
                            kvp.Value.Select(ch => new CharacterModel(ch)).ToList()))
                        .ToList();

                    return (lang.Group, isSelected, displayName, keyMappings);
                })
                .ToList();

            var groups = new List<ReferenceGroupModel>();

            // "Selected sets" synthetic group - selected character sets surfaced first.
            var selectedModels = allModels
                .Where(m => m.isSelected)
                .OrderBy(m => m.displayName, StringComparer.CurrentCulture)
                .Select(m => new ReferenceLanguageModel
                {
                    DisplayName = m.displayName,
                    IsSelected = true,
                    KeyMappings = m.keyMappings,
                })
                .ToList();

            if (selectedModels.Count > 0)
            {
                groups.Add(new ReferenceGroupModel
                {
                    GroupHeader = getLocalizedString("QuickAccent_ReferenceGuide_SelectedSets"),
                    Languages = selectedModels,
                });
            }

            // Remaining groups in GroupDisplayOrder order, unselected languages only.
            foreach (var group in CharacterMappings.GroupDisplayOrder)
            {
                var groupModels = allModels
                    .Where(m => m.Group == group && !m.isSelected)
                    .OrderBy(m => m.displayName, StringComparer.CurrentCulture)
                    .Select(m => new ReferenceLanguageModel
                    {
                        DisplayName = m.displayName,
                        IsSelected = false,
                        KeyMappings = m.keyMappings,
                    })
                    .ToList();

                if (groupModels.Count > 0)
                {
                    string groupResourceKey = group switch
                    {
                        LanguageGroup.Language => "QuickAccent_Group_Language",
                        LanguageGroup.Special => "QuickAccent_Group_Special",
                        LanguageGroup.UserDefined => "QuickAccent_Group_UserDefined",
                        _ => group.ToString(),
                    };

                    groups.Add(new ReferenceGroupModel
                    {
                        GroupHeader = getLocalizedString(groupResourceKey),
                        Languages = groupModels,
                    });
                }
            }

            return groups;
        }
    }
}
