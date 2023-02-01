// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json.Serialization;

using ManagedCommon;

using Wox.Plugin;

namespace Wox.Infrastructure.UserSettings
{
    public class PowerToysRunSettings : BaseModel
    {
        private string _hotkey = "Alt + Space";
        private string _previousHotkey = string.Empty;

        public string PreviousHotkey
        {
            get
            {
                return _previousHotkey;
            }
        }

        public string Hotkey
        {
            get
            {
                return _hotkey;
            }

            set
            {
                if (_hotkey != value)
                {
                    _previousHotkey = _hotkey;
                    _hotkey = value;
                    OnPropertyChanged(nameof(Hotkey));
                }
            }
        }

        private bool _useCentralizedKeyboardHook;

        public bool UseCentralizedKeyboardHook
        {
            get
            {
                return _useCentralizedKeyboardHook;
            }

            set
            {
                if (_useCentralizedKeyboardHook != value)
                {
                    _useCentralizedKeyboardHook = value;
                    OnPropertyChanged(nameof(UseCentralizedKeyboardHook));
                }
            }
        }

        private bool _searchQueryResultsWithDelay = true;

        public bool SearchQueryResultsWithDelay
        {
            get
            {
                return _searchQueryResultsWithDelay;
            }

            set
            {
                if (_searchQueryResultsWithDelay != value)
                {
                    _searchQueryResultsWithDelay = value;
                    OnPropertyChanged(nameof(SearchQueryResultsWithDelay));
                }
            }
        }

        private int _searchInputDelay = 150;

        private int _searchInputDelayFast = 30;

        private int _searchClickedItemWeight = 5;

        private bool _searchQueryTuningEnabled;

        private bool _searchWaitForSlowResults;

        public int SearchInputDelayFast
        {
            get
            {
                return _searchInputDelayFast;
            }

            set
            {
                if (_searchInputDelayFast != value)
                {
                    _searchInputDelayFast = value;
                    OnPropertyChanged(nameof(SearchInputDelayFast));
                }
            }
        }

        public int SearchInputDelay
        {
            get
            {
                return _searchInputDelay;
            }

            set
            {
                if (_searchInputDelay != value)
                {
                    _searchInputDelay = value;
                    OnPropertyChanged(nameof(SearchInputDelay));
                }
            }
        }

        public bool SearchQueryTuningEnabled
        {
            get
            {
                return _searchQueryTuningEnabled;
            }

            set
            {
                if (_searchQueryTuningEnabled != value)
                {
                    _searchQueryTuningEnabled = value;
                    OnPropertyChanged(nameof(SearchQueryTuningEnabled));
                }
            }
        }

        public bool SearchWaitForSlowResults
        {
            get
            {
                return _searchWaitForSlowResults;
            }

            set
            {
                if (_searchWaitForSlowResults != value)
                {
                    _searchWaitForSlowResults = value;
                    OnPropertyChanged(nameof(_searchWaitForSlowResults));
                }
            }
        }

        public int SearchClickedItemWeight
        {
            get
            {
                return _searchClickedItemWeight;
            }

            set
            {
                if (_searchClickedItemWeight != value)
                {
                    _searchClickedItemWeight = value;
                    OnPropertyChanged(nameof(SearchClickedItemWeight));
                }
            }
        }

        public string Language { get; set; } = "en";

        public Theme Theme { get; set; } = Theme.System;

        public StartupPosition StartupPosition { get; set; } = StartupPosition.Cursor;

        public string QueryBoxFont { get; set; } = FontFamily.GenericSansSerif.Name;

        public string QueryBoxFontStyle { get; set; }

        public string QueryBoxFontWeight { get; set; }

        public bool PTRunNonDelayedSearchInParallel { get; set; } = true;

        public string PTRunStartNewSearchAction { get; set; }

        public bool PTRSearchQueryFastResultsWithDelay { get; set; }

        // public bool PTRSearchQueryFastResultsWithPartialDelay { get; set; }
        public string QueryBoxFontStretch { get; set; }

        public string ResultFont { get; set; } = FontFamily.GenericSansSerif.Name;

        public string ResultFontStyle { get; set; }

        public string ResultFontWeight { get; set; }

        public string ResultFontStretch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether when false Alphabet static service will always return empty results
        /// </summary>
        public bool ShouldUsePinyin { get; set; }

        internal StringMatcher.SearchPrecisionScore QuerySearchPrecision { get; private set; } = StringMatcher.SearchPrecisionScore.Regular;

        [JsonIgnore]
        public string QuerySearchPrecisionString
        {
            get
            {
                return QuerySearchPrecision.ToString();
            }

            set
            {
                try
                {
                    var precisionScore = (StringMatcher.SearchPrecisionScore)Enum
                                            .Parse(typeof(StringMatcher.SearchPrecisionScore), value);

                    QuerySearchPrecision = precisionScore;
                    StringMatcher.Instance.UserSettingSearchPrecision = precisionScore;
                }
                catch (ArgumentException e)
                {
                    Wox.Plugin.Logger.Log.Exception("Failed to load QuerySearchPrecisionString value from Settings file", e, GetType());

                    QuerySearchPrecision = StringMatcher.SearchPrecisionScore.Regular;
                    StringMatcher.Instance.UserSettingSearchPrecision = StringMatcher.SearchPrecisionScore.Regular;

                    throw;
                }
            }
        }

        public bool AutoUpdates { get; set; }

        public double WindowLeft { get; set; }

        public double WindowTop { get; set; }

        private int _maxResultsToShow = 4;

        public int MaxResultsToShow
        {
            get
            {
                return _maxResultsToShow;
            }

            set
            {
                if (_maxResultsToShow != value)
                {
                    _maxResultsToShow = value;
                    OnPropertyChanged(nameof(MaxResultsToShow));
                }
            }
        }

        private int _activeTimes;

        public int ActivateTimes
        {
            get => _activeTimes;
            set
            {
                if (_activeTimes != value)
                {
                    _activeTimes = value;
                    OnPropertyChanged(nameof(ActivateTimes));
                }
            }
        }

        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; } = new ObservableCollection<CustomPluginHotkey>();

        public bool DontPromptUpdateMsg { get; set; }

        public bool EnableUpdateLog { get; set; }

        public bool StartWoxOnSystemStartup { get; set; } = true;

        public bool HideOnStartup { get; set; }

        private bool _hideNotifyIcon;

        public bool HideNotifyIcon
        {
            get
            {
                return _hideNotifyIcon;
            }

            set
            {
                _hideNotifyIcon = value;
                OnPropertyChanged();
            }
        }

        public bool LeaveCmdOpen { get; set; }

        public bool HideWhenDeactivated { get; set; } = true;

        public bool ClearInputOnLaunch { get; set; }

        public bool TabSelectsContextButtons { get; set; }

        public bool RememberLastLaunchLocation { get; set; }

        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public bool StartedFromPowerToysRunner { get; set; }

        public HttpProxy Proxy { get; set; } = new HttpProxy();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;
    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved,
    }
}
