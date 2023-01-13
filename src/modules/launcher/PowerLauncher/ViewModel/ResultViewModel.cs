// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using Wox.Infrastructure.Image;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        public enum ActivationType
        {
            Selection,
            Hover,
        }

        public ObservableCollection<ContextMenuItemViewModel> ContextMenuItems { get; } = new ObservableCollection<ContextMenuItemViewModel>();

        public ICommand ActivateContextButtonsHoverCommand { get; }

        public ICommand DeactivateContextButtonsHoverCommand { get; }

        public bool IsSelected { get; private set; }

        public bool IsHovered { get; private set; }

        private bool _areContextButtonsActive;

        public bool AreContextButtonsActive
        {
            get => _areContextButtonsActive;
            set
            {
                if (_areContextButtonsActive != value)
                {
                    _areContextButtonsActive = value;
                    OnPropertyChanged(nameof(AreContextButtonsActive));
                }
            }
        }

        private int _contextMenuSelectedIndex;

        public int ContextMenuSelectedIndex
        {
            get => _contextMenuSelectedIndex;
            set
            {
                if (_contextMenuSelectedIndex != value)
                {
                    _contextMenuSelectedIndex = value;
                    OnPropertyChanged(nameof(ContextMenuSelectedIndex));
                }
            }
        }

        public const int NoSelectionIndex = -1;

        public ResultViewModel(Result result, IMainViewModel mainViewModel)
        {
            if (result != null)
            {
                Result = result;
            }

            ContextMenuSelectedIndex = NoSelectionIndex;
            LoadContextMenu();

            ActivateContextButtonsHoverCommand = new RelayCommand(ActivateContextButtonsHoverAction);
            DeactivateContextButtonsHoverCommand = new RelayCommand(DeactivateContextButtonsHoverAction);
            MainViewModel = mainViewModel;
        }

        private void ActivateContextButtonsHoverAction(object sender)
        {
            ActivateContextButtons(ActivationType.Hover);
        }

        public void ActivateContextButtons(ActivationType activationType)
        {
            // Result does not contain any context menu items - we don't need to show the context menu ListView at all.
            if (ContextMenuItems.Count > 0)
            {
                AreContextButtonsActive = true;
            }
            else
            {
                AreContextButtonsActive = false;
            }

            if (activationType == ActivationType.Selection)
            {
                IsSelected = true;
                EnableContextMenuAcceleratorKeys();
            }
            else if (activationType == ActivationType.Hover)
            {
                IsHovered = true;
            }
        }

        private void DeactivateContextButtonsHoverAction(object sender)
        {
            DeactivateContextButtons(ActivationType.Hover);
        }

        public void DeactivateContextButtons(ActivationType activationType)
        {
            if (activationType == ActivationType.Selection)
            {
                IsSelected = false;
                DisableContextMenuAcceleratorkeys();
            }
            else if (activationType == ActivationType.Hover)
            {
                IsHovered = false;
            }

            // Result does not contain any context menu items - we don't need to show the context menu ListView at all.
            if (ContextMenuItems?.Count > 0)
            {
                AreContextButtonsActive = IsSelected || IsHovered;
            }
            else
            {
                AreContextButtonsActive = false;
            }
        }

        public void LoadContextMenu()
        {
            var results = PluginManager.GetContextMenusForPlugin(Result);
            ContextMenuItems.Clear();
            foreach (var r in results)
            {
                ContextMenuItems.Add(new ContextMenuItemViewModel(
                    r.PluginName,
                    r.Title,
                    r.Glyph,
                    r.FontFamily,
                    r.AcceleratorKey,
                    r.AcceleratorModifiers,
                    new RelayCommand(_ =>
                    {
                        bool hideWindow =
                            r.Action != null &&
                            r.Action(new ActionContext
                            {
                                SpecialKeyState = KeyboardHelper.CheckModifiers(),
                            });

                        if (hideWindow)
                        {
                            MainViewModel.Hide();
                        }
                    })));
            }
        }

        private void EnableContextMenuAcceleratorKeys()
        {
            foreach (var i in ContextMenuItems)
            {
                i.IsAcceleratorKeyEnabled = true;
            }
        }

        private void DisableContextMenuAcceleratorkeys()
        {
            foreach (var i in ContextMenuItems)
            {
                i.IsAcceleratorKeyEnabled = false;
            }
        }

        public ImageSource Image
        {
            get
            {
                var imagePath = Result.IcoPath;
                if (string.IsNullOrEmpty(imagePath) && Result.Icon != null)
                {
                    try
                    {
                        return Result.Icon();
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"IcoPath is empty and exception when calling Icon() for result <{Result.Title}> of plugin <{Result.PluginDirectory}>", e, GetType());
                        imagePath = ImageLoader.ErrorIconPath;
                    }
                }

                // will get here either when icoPath has value\icon delegate is null\when had exception in delegate
                return ImageLoader.Load(imagePath);
            }
        }

        // Returns false if we've already reached the last item.
        public bool SelectNextContextButton()
        {
            if (ContextMenuSelectedIndex == (ContextMenuItems.Count - 1))
            {
                ContextMenuSelectedIndex = NoSelectionIndex;
                return false;
            }

            ContextMenuSelectedIndex++;
            return true;
        }

        // Returns false if we've already reached the first item.
        public bool SelectPrevContextButton()
        {
            if (ContextMenuSelectedIndex == NoSelectionIndex)
            {
                return false;
            }

            ContextMenuSelectedIndex--;
            return true;
        }

        public void SelectLastContextButton()
        {
            ContextMenuSelectedIndex = ContextMenuItems.Count - 1;
        }

        public bool HasSelectedContextButton()
        {
            var isContextSelected = ContextMenuSelectedIndex != NoSelectionIndex;
            return isContextSelected;
        }

        /// <summary>
        ///  Triggers the action on the selected context button
        /// </summary>
        /// <returns>False if there is nothing selected, otherwise true</returns>
        public bool ExecuteSelectedContextButton()
        {
            if (HasSelectedContextButton())
            {
                ContextMenuItems[ContextMenuSelectedIndex].Command.Execute(null);
                return true;
            }

            return false;
        }

        public Result Result { get; }

        public IMainViewModel MainViewModel { get; }

        public override bool Equals(object obj)
        {
            var r = obj as ResultViewModel;
            if (r != null)
            {
                return Result.Equals(r.Result);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Result.GetHashCode();
        }

        public string SearchBoxDisplayText()
        {
            return Result.QueryTextDisplay;
        }

        public override string ToString()
        {
            // Using CurrentCulture since this is user facing
            var contextMenuInfo = ContextMenuItems.Count > 0 ? string.Format(CultureInfo.CurrentCulture, "{0} {1}", ContextMenuItems.Count, Properties.Resources.ContextMenuItemsAvailable) : string.Empty;
            return string.Format(CultureInfo.CurrentCulture, "{0}, {1}", Result.ToString(), contextMenuInfo);
        }
    }
}
