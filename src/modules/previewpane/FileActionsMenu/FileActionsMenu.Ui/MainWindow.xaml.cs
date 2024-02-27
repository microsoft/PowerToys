// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FileActionsMenu.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using WinUIEx;

namespace FileActionsMenu.Ui
{
    public partial class MainWindow : Window
    {
        private readonly CheckedMenuItemsDictionary _checkableMenuItemsIndex = [];

        private MenuFlyout _menu;

        private IAction[] _actions =
        [
            new CloseAction(),
        ];

        private bool _actionStarted;
        private bool _cancelClose;

        public MainWindow(string[] selectedItems)
        {
            InitializeComponent();

            this.SetWindowOpacity(0);

            string[] pluginPaths = Directory.EnumerateDirectories((Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException()) + "\\..\\FileActionsMenuPlugins").ToArray();
            foreach (string pluginPath in pluginPaths)
            {
                Assembly plugin = Assembly.LoadFrom(Directory.EnumerateFiles(pluginPath).First(file => Path.GetFileName(file).StartsWith("PowerToys.FileActionsMenu.Plugins", StringComparison.InvariantCultureIgnoreCase) && Path.GetFileName(file).EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)));
                plugin.GetExportedTypes().Where(type => type.GetInterfaces().Any(i => i.FullName?.EndsWith("IFileActionsMenuPlugin", StringComparison.InvariantCulture) ?? false)).ToList().ForEach(type =>
                {
                    dynamic pluginInstance = Activator.CreateInstance(type)!;
                    Array.ForEach((IAction[])pluginInstance.TopLevelMenuActions, action => Array.Resize(ref _actions, _actions.Length + 1));
                    pluginInstance.TopLevelMenuActions.CopyTo(_actions, _actions.Length - pluginInstance.TopLevelMenuActions.Length);
                });
            }

            _menu = new MenuFlyout();

            _menu.Items.Add(new MenuFlyoutItem()
            {
                Text = "PowerToys File Actions menu",
                IsEnabled = false,
            });
            _menu.Items.Add(new MenuFlyoutSeparator());

            Array.Sort(_actions, (a, b) => a.Category.CompareTo(b.Category));
            int currentCategory = -1;
            void HandleItems(IAction[] actions, object cm, bool firstLayer = true)
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
                            (cm as MenuFlyout)!.Items.Add(new MenuFlyoutSeparator());
                        }

                        MenuFlyoutItemBase menuItem;
                        menuItem = new MenuFlyoutItem()
                        {
                            Text = action.Header,
                        };

                        if (action.Icon != null)
                        {
                            if (action.Icon is BitmapIcon bi)
                            {
                                bi.ShowAsMonochrome = false;
                            }

                            ((MenuFlyoutItem)menuItem).Icon = action.Icon;
                        }

                        if (action is IActionAndRequestCheckedMenuItems requestCheckedMenuItems)
                        {
                            requestCheckedMenuItems.CheckedMenuItemsDictionary = _checkableMenuItemsIndex;
                        }

                        if (action.Type == IAction.ItemType.SingleItem)
                        {
                            if (cm is MenuFlyout flyout)
                            {
                                ((MenuFlyoutItem)menuItem).Click += async (sender, args) =>
                                {
                                    _actionStarted = true;
                                    await action.Execute(sender, args);
                                    Close();
                                };
                                flyout.Items.Add(menuItem);
                            }
                            else if (cm is MenuFlyoutSubItem item)
                            {
                                ((MenuFlyoutItem)menuItem).Click += async (sender, args) =>
                                {
                                    _actionStarted = true;
                                    await action.Execute(sender, args);
                                    Close();
                                };
                                item.Items.Add(menuItem);
                            }
                        }
                        else if (action.Type == IAction.ItemType.HasSubMenu)
                        {
                            MenuFlyoutSubItem subItem = new()
                            {
                                Text = action.Header,
                            };
                            if (action.Icon != null)
                            {
                                subItem.Icon = action.Icon;
                            }

                            HandleItems(action.SubMenuItems!, subItem, false);

                            if (cm is MenuFlyout menuFlyout)
                            {
                                menuFlyout.Items.Add(subItem);
                            }
                            else if (cm is MenuFlyoutSubItem menuFlyoutSub)
                            {
                                menuFlyoutSub.Items.Add(subItem);
                            }
                        }
                        else if (action.Type == IAction.ItemType.Separator)
                        {
                            if (cm is MenuFlyout menuFlyout)
                            {
                                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                            }
                            else if (cm is MenuFlyoutSubItem menuFlyoutSubItem)
                            {
                                menuFlyoutSubItem.Items.Add(new MenuFlyoutSeparator());
                            }
                        }
                        else if (action.Type == IAction.ItemType.Checkable)
                        {
                            if (action is not ICheckableAction checkableAction || checkableAction.CheckableGroupUUID == null)
                            {
                                throw new InvalidDataException("Action is checkable but does not implement ICheckableAction or IChechableAction.CheckableGroupUUID is null");
                            }

                            ToggleMenuFlyoutItem toggleMenuItem = new()
                            {
                                Text = checkableAction.Header,
                                IsChecked = checkableAction.IsCheckedByDefault,
                            };

                            if (!_checkableMenuItemsIndex.TryGetValue(checkableAction.CheckableGroupUUID, out List<(MenuFlyoutItemBase, IAction)>? value))
                            {
                                value = [];
                                _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID] = value;
                            }

                            value.Add((toggleMenuItem, checkableAction));

                            toggleMenuItem.Click += (sender, args) =>
                            {
                                _cancelClose = true;

                                if (checkableAction.IsChecked)
                                {
                                    return;
                                }

                                checkableAction.IsChecked = toggleMenuItem.IsChecked;

                                foreach ((MenuFlyoutItemBase menuItem, IAction action) in _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID])
                                {
                                    if (menuItem is ToggleMenuFlyoutItem toggle)
                                    {
                                        if (toggle != toggleMenuItem)
                                        {
                                            toggle.IsChecked = false;
                                            toggle.IsChecked = false;
                                        }
                                    }
                                }
                            };

                            if (cm is MenuFlyout menuFlyout)
                            {
                                menuFlyout.Items.Add(toggleMenuItem);
                            }
                            else if (cm is MenuFlyoutSubItem menuFlyoutSubItem)
                            {
                                menuFlyoutSubItem.Items.Add(toggleMenuItem);
                            }
                        }
                        else
                        {
                            throw new InvalidDataException("Unknown value for IAction.Type");
                        }
                    }
                }
            }

            HandleItems(_actions, _menu);
        }

        public void Window_Activated(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.SetForegroundWindow();
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
            this.Maximize();
            _menu.ShowAt((UIElement)sender, new Point(Cursor.Position.X, Cursor.Position.Y));
            _menu.Closing += (s, e) =>
            {
                // Keep open if user clicked on a checkable item
                if (_cancelClose)
                {
                    e.Cancel = true;
                    _cancelClose = false;
                    return;
                }

                if (!_actionStarted)
                {
                    Close();
                }
            };
        }
    }
}
