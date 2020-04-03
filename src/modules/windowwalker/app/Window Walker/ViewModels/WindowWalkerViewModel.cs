// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Microsoft.Win32;
using WindowWalker.Components;
using WindowWalker.MVVMHelpers;

namespace WindowWalker.ViewModels
{
    internal class WindowWalkerViewModel : PropertyChangedBase
    {
        private readonly HotKeyHandler _hotKeyHandler;
        private readonly List<string> _hints = new List<string>()
        {
            "search for running processes or windows...",
            // "you can reinvoke this app using CTRL + WIN",
        };

        private string _searchText = string.Empty;
        private List<SearchResult> _results = new List<SearchResult>();
        private SearchResult _selectedWindow;
        private bool _windowVisibility = true;
        private string _hint = string.Empty;
        private int _hintCounter = 0;

        private void WireCommands()
        {
            SwitchToSelectedWindowCommand = new RelayCommand(SwitchToSelectedWindow)
            {
                IsEnabled = true,
            };
            WindowNavigateToNextResultCommand = new RelayCommand(WindowNavigateToNextResult)
            {
                IsEnabled = true,
            };
            WindowNavigateToPreviousResultCommand = new RelayCommand(WindowNavigateToPreviousResult)
            {
                IsEnabled = true,
            };
            WindowHideCommand = new RelayCommand(WindowHide)
            {
                IsEnabled = true,
            };
            WindowShowCommand = new RelayCommand(WindowShow)
            {
                IsEnabled = true,
            };
        }

        public string SearchText
        {
            get => _searchText;

            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    SearchController.Instance.SearchText = value;
                    NotifyPropertyChanged("SearchText");
                }
            }
        }

        public string Hint
        {
            get => _hint;

            set
            {
                if (_hint != value)
                {
                    _hint = value;
                    NotifyPropertyChanged("Hint");
                }
            }
        }

        public List<SearchResult> Results
        {
            get => _results;

            set
            {
                if (_results != value)
                {
                    _results = value;
                    NotifyPropertyChanged("Results");
                }
            }
        }

        public SearchResult SelectedWindowResult
        {
            get => _selectedWindow;
            set
            {
                if (_selectedWindow != value)
                {
                    _selectedWindow = value;
                    WindowResultSelected();
                    NotifyPropertyChanged("SelectedWindowResult");
                }
            }
        }

        public IntPtr Hwnd { get; private set; }

        public bool WindowVisibility
        {
            get
            {
                return _windowVisibility;
            }

            set
            {
                if (_windowVisibility != value)
                {
                    _windowVisibility = value;
                    NotifyPropertyChanged("WindowVisibility");
                }
            }
        }

        public RelayCommand SwitchToSelectedWindowCommand
        {
            get;
            private set;
        }

        public RelayCommand WindowNavigateToNextResultCommand
        {
            get;
            private set;
        }

        public RelayCommand WindowNavigateToPreviousResultCommand
        {
            get;
            private set;
        }

        public RelayCommand WindowHideCommand
        {
            get;
            private set;
        }

        public RelayCommand WindowShowCommand
        {
            get;
            private set;
        }

        public WindowWalkerViewModel(System.Windows.Window mainWindow)
        {
            SearchController.Instance.OnSearchResultUpdate += SearchResultUpdated;
            OpenWindows.Instance.UpdateOpenWindowsList();
            Hwnd = new WindowInteropHelper(mainWindow).Handle;
            LivePreview.SetWindowExlusionFromLivePreview(Hwnd);

            _hotKeyHandler = new HotKeyHandler(mainWindow);
            _hotKeyHandler.OnHotKeyPressed += HotKeyPressedHandler;

            // _hints.AddRange(Commands.GetTips());
            Hint = _hints[_hintCounter];

            WireCommands();
        }

        private void HotKeyPressedHandler(object sender, EventArgs e)
        {
            if (SearchText == string.Empty && WindowVisibility)
            {
                WindowHide();
            }
            else
            {
                WindowShow();
            }
        }

        private void WindowResultSelected()
        {
            if (SelectedWindowResult != null)
            {
                LivePreview.ActivateLivePreview(SelectedWindowResult.Result.Hwnd, Hwnd);
            }
        }

        private void WindowNavigateToPreviousResult()
        {
            if (SelectedWindowResult == null && Results.Count > 0)
            {
                SelectedWindowResult = Results.Last();
                return;
            }

            if (Results.Count > 0)
            {
                SelectedWindowResult = Results[(Results.IndexOf(SelectedWindowResult) + Results.Count - 1) % Results.Count];
            }
        }

        private void WindowNavigateToNextResult()
        {
            if (SelectedWindowResult == null && Results.Count > 0)
            {
                SelectedWindowResult = Results.First();
                return;
            }

            if (Results.Count > 0)
            {
                SelectedWindowResult = Results[(Results.IndexOf(SelectedWindowResult) + 1) % Results.Count];
            }
        }

        private void WindowHide()
        {
            LivePreview.DeactivateLivePreview();
            WindowVisibility = false;

            // ApplicationUpdates.InstallUpdateSyncWithInfo();
        }

        private void WindowShow()
        {
            _hintCounter = (_hintCounter + 1) % _hints.Count;
            Hint = _hints[_hintCounter];

            SearchText = string.Empty;
            OpenWindows.Instance.UpdateOpenWindowsList();
            LivePreview.DeactivateLivePreview();
            WindowVisibility = true;
            InteropAndHelpers.SetForegroundWindow(Hwnd);
        }

        public void SwitchToSelectedWindow()
        {
            if (SelectedWindowResult != null)
            {
                LivePreview.DeactivateLivePreview();
                SelectedWindowResult.Result.SwitchToWindow();
                WindowHide();
            }
            else if (Results != null && Results.Count > 0)
            {
                LivePreview.DeactivateLivePreview();
                Results.First().Result.SwitchToWindow();
                WindowHide();
            }
        }

        private void SearchResultUpdated(object sender, SearchController.SearchResultUpdateEventArgs e)
        {
            Results = SearchController.Instance.SearchMatches;
        }
    }
}
