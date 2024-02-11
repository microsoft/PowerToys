// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileActionsMenu.Ui.Actions;
using FileActionsMenu.Ui.Actions.CopyPath;
using FileActionsMenu.Ui.Actions.Hashes.Hashes;
using Wpf.Ui.Controls;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace FileActionsMenu.Ui
{
    public partial class MainWindow : FluentWindow
    {
        private static readonly IAction[] Actions =
        [
            new CopyPath(),
                new Hashes(),
                new FileLocksmith(),
                new CopyImageToClipboard(),
                new CopyTo(),
                new PowerRename(),
                new ImageResizer(),
                new MoveTo(),
                new NewFolderWithSelection(),
                new Close(),
                new CopyImageFromClipboardToFolder(),
            ];

        private readonly Dictionary<string, List<(MenuItem, IAction)>> _checkableMenuItemsIndex = [];

        public MainWindow(string[] selectedItems)
        {
            InitializeComponent();

            // WindowStyle = WindowStyle.None;
            // AllowsTransparency = true;

            // Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.None);
            ContextMenu cm = (ContextMenu)FindResource("Menu");
            Array.Sort(Actions, (a, b) => a.Category.CompareTo(b.Category));

            int currentCategory = -1;

            void HandleItems(IAction[] actions, ItemsControl cm, bool firstLayer = true)
            {
                foreach (IAction action in actions)
                {
                    action.SelectedItems = selectedItems;
                    if (action.IsVisible)
                    {
                        // Categories only apply to the first layer of items
                        if (firstLayer && action.Category != currentCategory)
                        {
                            currentCategory = action.Category;
                            cm.Items.Add(new System.Windows.Controls.Separator());
                        }

                        MenuItem menuItem = new()
                        {
                            Header = action.Header,
                        };

                        if (action.Icon != null)
                        {
                            menuItem.Icon = action.Icon;
                            if (menuItem.Icon is FontIcon fontIcon)
                            {
                                fontIcon.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
                            }
                        }

                        if (action.Type == IAction.ItemType.HasSubMenu)
                        {
                            HandleItems(action.SubMenuItems!, menuItem, false);
                        }
                        else if (action.Type == IAction.ItemType.Separator)
                        {
                            cm.Items.Add(new System.Windows.Controls.Separator());
                            continue;
                        }
                        else if (action.Type == IAction.ItemType.Checkable && action is ICheckableAction checkableAction)
                        {
                            if (checkableAction.CheckableGroupUUID is not null)
                            {
                                if (!_checkableMenuItemsIndex.TryGetValue(checkableAction.CheckableGroupUUID, out List<(MenuItem, IAction)>? value))
                                {
                                    value = [];
                                    _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID] = value;
                                }

                                value.Add((menuItem, action));
                            }

                            menuItem.IsCheckable = true;

                            menuItem.IsChecked = checkableAction.IsCheckedByDefault;

                            menuItem.StaysOpenOnClick = true;

                            RoutedEventHandler? uncheckedHandler = null;

                            void CheckedHandler(object sender, RoutedEventArgs e)
                            {
                                menuItem.Unchecked -= uncheckedHandler;
                                if (checkableAction.CheckableGroupUUID is not null)
                                {
                                    _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID].ForEach((m) =>
                                    {
                                        if (m.Item1 != menuItem)
                                        {
                                            m.Item1.Unchecked -= uncheckedHandler;
                                            m.Item1.IsChecked = false;
                                            m.Item1.Unchecked += uncheckedHandler;
                                        }
                                    });
                                }

                                checkableAction.IsChecked = true;
                                menuItem.Unchecked += uncheckedHandler;
                            }

                            uncheckedHandler = (object sender, RoutedEventArgs e) =>
                            {
                                menuItem.Checked -= CheckedHandler;
                                if (checkableAction.CheckableGroupUUID is not null)
                                {
                                    int count = _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID].Count((m) => m.Item1.IsChecked);
                                    menuItem.IsChecked = count == 0;
                                }

                                menuItem.Checked += CheckedHandler;
                            };

                            menuItem.Checked += CheckedHandler;

                            menuItem.Unchecked += uncheckedHandler;
                        }
                        else
                        {
                            menuItem.Click += async (object sender, RoutedEventArgs e) =>
                            {
                                if (action is IActionAndRequestCheckedMenuItems actionAndRequestCheckedMenuItems)
                                {
                                    actionAndRequestCheckedMenuItems.CheckedMenuItemsDictionary = _checkableMenuItemsIndex;
                                }

                                await action.Execute(sender, e);
                                Environment.Exit(0);
                            };
                        }

                        cm.Items.Add(menuItem);
                    }
                }
            }

            HandleItems(Actions, cm);

            cm.IsOpen = true;
            /*cm.Closed += (sender, args) => Close();*/
        }
    }
}
