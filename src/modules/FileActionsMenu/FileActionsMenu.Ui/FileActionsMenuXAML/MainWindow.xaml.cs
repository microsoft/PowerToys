// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using WinRT.Interop;
using WinUIEx;

namespace FileActionsMenu.Ui
{
    public partial class MainWindow : Window
    {
        private readonly CheckedMenuItemsDictionary _checkableMenuItemsIndex = [];

        private readonly MenuFlyout _menu;

        private IAction[] _actions =
        [
            new CloseAction(),
        ];

        private bool _actionStarted;
        private bool _cancelClose;
        private bool _cancelCheckableEvent;

        public MainWindow(string[] selectedItems)
        {
            InitializeComponent();

            this.SetWindowOpacity(0);

            string[] ignoredDirectories = ["FileActionsMenu.Helpers", "FileActionsMenu.Interfaces", "runtimes", "Peek.Common", "PowerToys.FileActionsMenu.Plugins"];

            string[] pluginPaths = Directory.EnumerateDirectories((Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException()) + "\\..\\FileActionsMenuPlugins").Where(folderName => !ignoredDirectories.Contains(Path.GetFileName(folderName))).ToArray();
            foreach (string pluginPath in pluginPaths)
            {
                try
                {
                    Assembly plugin = Assembly.LoadFrom(Directory.EnumerateFiles(pluginPath).First(file => Path.GetFileName(file).StartsWith("PowerToys.FileActionsMenu.Plugins", StringComparison.InvariantCultureIgnoreCase) && Path.GetFileName(file).EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)));
                    plugin.GetExportedTypes().Where(type => type.GetInterfaces().Any(i => i.FullName?.EndsWith("IFileActionsMenuPlugin", StringComparison.InvariantCulture) ?? false)).ToList().ForEach(type =>
                    {
                        dynamic pluginInstance = Activator.CreateInstance(type)!;
                        Array.ForEach((IAction[])pluginInstance.TopLevelMenuActions, action => Array.Resize(ref _actions, _actions.Length + 1));
                        pluginInstance.TopLevelMenuActions.CopyTo(_actions, _actions.Length - pluginInstance.TopLevelMenuActions.Length);
                    });
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show(ResourceHelper.GetResource("InvalidPlugin") + pluginPath, ResourceHelper.GetResource("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            PowerToysTelemetry.Log.WriteEvent(new FileActionsMenuInvokedEvent()
            {
                LoadedPluginsCount = pluginPaths.Length,
            });

            _menu = new MenuFlyout();

            _menu.Items.Add(new MenuFlyoutItem()
            {
                Text = ResourceHelper.GetResource("ModuleName"),
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
                            Text = action.Title,
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
                                    await ExecuteActionOfAction(action, sender, args);
                                };
                                flyout.Items.Add(menuItem);
                            }
                            else if (cm is MenuFlyoutSubItem item)
                            {
                                ((MenuFlyoutItem)menuItem).Click += async (sender, args) =>
                                {
                                    await ExecuteActionOfAction(action, sender, args);
                                };
                                item.Items.Add(menuItem);
                            }
                        }
                        else if (action.Type == IAction.ItemType.HasSubMenu)
                        {
                            MenuFlyoutSubItem subItem = new()
                            {
                                Text = action.Title,
                            };
                            if (action.Icon != null)
                            {
                                if (action.Icon is BitmapIcon bi)
                                {
                                    bi.ShowAsMonochrome = false;
                                }

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
                                throw new InvalidDataException("Action is checkable but does not implement ICheckableAction or ICheckableAction.CheckableGroupUUID is null");
                            }

                            ToggleMenuFlyoutItem toggleMenuItem = new()
                            {
                                Text = checkableAction.Title,
                                IsChecked = checkableAction.IsCheckedByDefault,
                            };

                            checkableAction.IsChecked = checkableAction.IsCheckedByDefault;

                            if (checkableAction.Icon != null)
                            {
                                if (action.Icon is BitmapIcon bi)
                                {
                                    bi.ShowAsMonochrome = false;
                                }

                                toggleMenuItem.Icon = checkableAction.Icon;
                            }

                            if (!_checkableMenuItemsIndex.TryGetValue(checkableAction.CheckableGroupUUID, out List<(MenuFlyoutItemBase, IAction)>? value))
                            {
                                value = [];
                                _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID] = value;
                            }

                            value.Add((toggleMenuItem, checkableAction));

                            toggleMenuItem.Click += (sender, args) =>
                            {
                                if (_cancelCheckableEvent)
                                {
                                    return;
                                }

                                _cancelClose = true;
                                _cancelCheckableEvent = true;

                                checkableAction.IsChecked = toggleMenuItem.IsChecked;

                                foreach ((MenuFlyoutItemBase menuItem, IAction action) in _checkableMenuItemsIndex[checkableAction.CheckableGroupUUID])
                                {
                                    if (menuItem is ToggleMenuFlyoutItem toggle)
                                    {
                                        if (toggle != toggleMenuItem)
                                        {
                                            toggle.IsChecked = false;
                                        }
                                    }
                                }

                                toggleMenuItem.IsChecked = true;
                                _cancelCheckableEvent = false;
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

        private async Task ExecuteActionOfAction(IAction action, object sender, RoutedEventArgs e)
        {
            _actionStarted = true;
            try
            {
                Task closeAction = action.Execute(sender, e);

                Close();

                await closeAction;

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error executing the action: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public void Window_Activated(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.SetForegroundWindow();
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
            this.Maximize();

            if (GetCursorPos(out POINT p))
            {
                var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(this));
                var scaleFactor = dpi / 96.0;
                p.X = (int)(p.X / scaleFactor);
                p.Y = (int)(p.Y / scaleFactor);
                _menu.ShowAt((UIElement)sender, new Windows.Foundation.Point(p.X, p.Y));
            }
            else
            {
                _menu.ShowAt((UIElement)sender, new Windows.Foundation.Point(0, 0));
            }

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
                    Environment.Exit(0);
                }
            };
        }
    }
}
