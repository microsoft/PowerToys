// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using TopToolbar.Actions;
using TopToolbar.Models;
using TopToolbar.Providers;
using TopToolbar.Providers.Configuration;
using TopToolbar.Providers.External.Mcp;
using TopToolbar.Services;
using TopToolbar.ViewModels;
using WinUIEx;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

namespace TopToolbar
{
    public sealed partial class ToolbarWindow : WindowEx, IDisposable
    {
        private const int TriggerZoneHeight = 2;
        private readonly ToolbarConfigService _configService;
        private readonly ActionProviderRuntime _providerRuntime;
        private readonly ActionProviderService _providerService;
        private readonly ActionContextFactory _contextFactory;
        private readonly ToolbarActionExecutor _actionExecutor;
        private readonly List<IDisposable> _providerDisposables = new();
        private readonly ToolbarViewModel _vm;

        private readonly TopToolbar.Stores.ToolbarStore _store = new();
        private readonly Dictionary<string, ButtonGroup> _groupMap = new(StringComparer.OrdinalIgnoreCase);
        private int _lastPartialUpdateTick;
        private Timer _monitorTimer;
        private Timer _configWatcherDebounce;
        private bool _isVisible;
        private bool _builtConfigOnce;
        private IntPtr _hwnd;
        private bool _initializedLayout;
        private Border _toolbarContainer;
        private ScrollViewer _scrollViewer;
        private FileSystemWatcher _configWatcher;

        public ToolbarWindow()
        {
            _configService = new ToolbarConfigService();
            _contextFactory = new ActionContextFactory();
            _providerRuntime = new ActionProviderRuntime();
            _providerService = new ActionProviderService(_providerRuntime);
            _actionExecutor = new ToolbarActionExecutor(_providerService, _contextFactory);
            _vm = new ToolbarViewModel(_configService, _providerService, _contextFactory);
            RegisterProviders();

            _store.StoreChanged += (s, e) =>
            {
                // If a partial update just occurred, suppress immediate full rebuild to avoid duplicate visuals.
                if (Environment.TickCount - _lastPartialUpdateTick < 50)
                {
                    return;
                }

                try
                {
                    BuildToolbarFromStore();
                    ResizeToContent();
                }
                catch
                {
                }
            };

            _store.StoreChangedDetailed += (s, e) =>
            {
                try
                {
                    if (e == null || e.Kind == TopToolbar.Stores.StoreChangeKind.Reset || string.IsNullOrWhiteSpace(e.GroupId))
                    {
                        BuildToolbarFromStore();
                        ResizeToContent();
                        return;
                    }

                    // Attempt partial update for single group
                    if (!DispatcherQueue.TryEnqueue(() =>
                    {
                        BuildOrReplaceSingleGroup(e.GroupId);
                        _lastPartialUpdateTick = Environment.TickCount;
                    }))
                    {
                        BuildOrReplaceSingleGroup(e.GroupId);
                        _lastPartialUpdateTick = Environment.TickCount;
                    }
                }
                catch
                {
                }
            };

            _providerRuntime.ProvidersChanged += async (_, args) =>
            {
                if (args == null)
                {
                    return;
                }

                // Only handle WorkspaceProvider for now (other providers not yet dynamic)
                if (!string.Equals(args.ProviderId, "WorkspaceProvider", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    var kindsNeedingGroup = args.Kind == ProviderChangeKind.ActionsUpdated ||
                                            args.Kind == ProviderChangeKind.ActionsAdded ||
                                            args.Kind == ProviderChangeKind.ActionsRemoved ||
                                            args.Kind == ProviderChangeKind.GroupUpdated ||
                                            args.Kind == ProviderChangeKind.BulkRefresh ||
                                            args.Kind == ProviderChangeKind.Reset ||
                                            args.Kind == ProviderChangeKind.ProviderRegistered;

                    if (!kindsNeedingGroup)
                    {
                        return; // other change kinds (progress, execution) not yet surfaced
                    }

                    // Build new group off the UI thread
                    var ctx = new ActionContext();
                    ButtonGroup newGroup;
                    try
                    {
                        newGroup = await _providerService.CreateGroupAsync("WorkspaceProvider", ctx, CancellationToken.None);
                    }
                    catch
                    {
                        // TODO: log: failed to create workspace group
                        return;
                    }

                    void ApplyStore()
                    {
                        try
                        {
                            _store.UpsertProviderGroup(newGroup);
                        }
                        catch
                        {
                            // TODO: log: store upsert failed
                        }
                    }

                    if (!DispatcherQueue.TryEnqueue(ApplyStore))
                    {
                        ApplyStore();
                    }
                }
                catch (Exception)
                {
                    // TODO: log: provider change handling wrapper failure
                }
            };

            Title = "Top Toolbar";

            // Make window background completely transparent
            this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop(
                Windows.UI.Color.FromArgb(0, 0, 0, 0));

            // Create the toolbar content programmatically with transparent root
            CreateToolbarShell();

            // Apply styles when content is loaded
            _toolbarContainer.Loaded += (s, e) =>
            {
                if (!_initializedLayout)
                {
                    _hwnd = this.GetWindowHandle();
                    ApplyTransparentBackground();
                    ApplyFramelessStyles();
                    ResizeToContent();
                    PositionAtTopCenter();
                    AppWindow.Hide();
                    _isVisible = false;
                    _initializedLayout = true;
                }
            };

            // Apply styles immediately after activation as backup
            this.Activated += (s, e) => MakeTopMost();

            StartMonitoring();
            StartWatchingConfig();

            // Load config and build UI when window activates
            this.Activated += async (s, e) =>
            {
                if (_builtConfigOnce)
                {
                    return;
                }

                await _vm.LoadAsync(this.DispatcherQueue);

                // Ensure UI-thread access for XAML object tree
                DispatcherQueue.TryEnqueue(() =>
                {
                    SyncStaticGroupsIntoStore();
                    BuildToolbarFromStore();
                    ResizeToContent();
                    PositionAtTopCenter();
                    _builtConfigOnce = true;
                });
            };
        }

        private void RegisterProviders()
        {
            try
            {
                var configService = new ProviderConfigService();
                foreach (var config in configService.LoadConfigs())
                {
                    if (config == null || string.IsNullOrWhiteSpace(config.Id))
                    {
                        continue;
                    }

                    try
                    {
                        var provider = CreateProvider(config);
                        _providerRuntime.RegisterProvider(provider);

                        if (provider is IDisposable disposable)
                        {
                            _providerDisposables.Add(disposable);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var workspaceProvider = new WorkspaceProvider();
                _providerRuntime.RegisterProvider(workspaceProvider);
            }
            catch (Exception)
            {
            }
        }

        private static IActionProvider CreateProvider(ProviderConfig config)
        {
            if (config?.External?.Type == ExternalProviderType.Mcp)
            {
                return new McpActionProvider(config);
            }

            return new ConfiguredActionProvider(config);
        }

        private void CreateToolbarShell()
        {
            // Create a completely transparent root container with symmetric padding for shadow
            var rootGrid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                UseLayoutRounding = true,
                IsHitTestVisible = true,
                Padding = new Thickness(12), // Symmetric padding for shadow space
            };

            // Create the toolbar content with modern macOS-style design and default shadow
            var border = new Border
            {
                Name = "ToolbarContainer",
                CornerRadius = new CornerRadius(12),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                Height = 75,
                Padding = new Thickness(12, 6, 12, 6),
                VerticalAlignment = VerticalAlignment.Center, // Back to center
                HorizontalAlignment = HorizontalAlignment.Center,
                UseLayoutRounding = true,
                IsHitTestVisible = true,     // the pill remains interactive
            };

            // Apply default shadow
            var themeShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
            border.Shadow = themeShadow;

            var mainStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = true,
                Name = "MainStack",
            };

            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Enabled,
                VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled,
                Content = mainStack,
            };

            border.Child = _scrollViewer;
            rootGrid.Children.Add(border);
            this.Content = rootGrid;
            _toolbarContainer = border;
        }

        private void StartWatchingConfig()
        {
            try
            {
                var path = _configService.ConfigPath;
                var dir = Path.GetDirectoryName(path);
                var file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
                {
                    return;
                }

                _configWatcherDebounce = new Timer(250) { AutoReset = false };
                _configWatcherDebounce.Elapsed += async (s, e) =>
                {
                    await _vm.LoadAsync(this.DispatcherQueue);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SyncStaticGroupsIntoStore();
                        BuildToolbarFromStore();
                        ResizeToContent();
                    });
                };

                _configWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                FileSystemEventHandler onChanged = (s, e) =>
                {
                    _configWatcherDebounce.Stop();
                    _configWatcherDebounce.Start();
                };
                RenamedEventHandler onRenamed = (s, e) =>
                {
                    _configWatcherDebounce.Stop();
                    _configWatcherDebounce.Start();
                };

                _configWatcher.Changed += onChanged;
                _configWatcher.Created += onChanged;
                _configWatcher.Deleted += onChanged;
                _configWatcher.Renamed += onRenamed;
            }
            catch
            {
                // ignore watcher failures
            }
        }

        // Sync static (config) groups into the central store so subsequent dynamic rebuilds retain them.
        private void SyncStaticGroupsIntoStore()
        {
            try
            {
                foreach (var g in _vm.Groups)
                {
                    if (g == null)
                    {
                        continue;
                    }

                    // Workspace group (dynamic) will arrive via provider path
                    if (string.Equals(g.Id, "WorkspaceProvider", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Upsert static group into store (reuse provider upsert since it is id-based)
                    _store.UpsertProviderGroup(g);
                }
            }
            catch
            {
            }
        }

        private void BuildToolbarFromStore()
        {
            StackPanel mainStack = (_toolbarContainer?.Child as ScrollViewer)?.Content as StackPanel
                                   ?? _toolbarContainer?.Child as StackPanel;
            if (mainStack == null)
            {
                return;
            }

            mainStack.Children.Clear();

            // Filter enabled groups and buttons
            var activeGroups = _store.Groups
                .Where(g => g != null && g.IsEnabled)
                .Select(g => new { Group = g, EnabledButtons = g.Buttons.Where(b => b.IsEnabled).ToList() })
                .Where(x => x.EnabledButtons.Count > 0)
                .ToList();

            for (int gi = 0; gi < activeGroups.Count; gi++)
            {
                var group = activeGroups[gi].Group;
                var enabledButtons = activeGroups[gi].EnabledButtons;

                var groupContainer = new Border
                {
                    CornerRadius = new CornerRadius(15),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 0, 0)),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(2, 0, 2, 0),
                    Tag = group.Id,
                };

                var groupShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
                groupContainer.Shadow = groupShadow;
                groupContainer.Translation = new System.Numerics.Vector3(0, 0, 1);

                var groupPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
                foreach (var btn in enabledButtons)
                {
                    var iconButton = CreateIconButton(group, btn);
                    groupPanel.Children.Add(iconButton);
                }

                groupContainer.Child = groupPanel;
                mainStack.Children.Add(groupContainer);

                if (gi != activeGroups.Count - 1)
                {
                    var separatorContainer = new Border
                    {
                        Width = 1,
                        Height = 24,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(50, 0, 0, 0)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 8, 0),
                        IsHitTestVisible = false,
                        CornerRadius = new CornerRadius(0.5),
                    };
                    var separatorShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
                    separatorContainer.Shadow = separatorShadow;
                    separatorContainer.Translation = new System.Numerics.Vector3(0, 0, 2);
                    mainStack.Children.Add(separatorContainer);
                }
            }

            var settingsSeparatorContainer = new Border
            {
                Width = 1,
                Height = 24,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(50, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                IsHitTestVisible = false,
                CornerRadius = new CornerRadius(0.5),
                Tag = "__SETTINGS_SEPARATOR__",
            };
            var settingsSeparatorShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
            settingsSeparatorContainer.Shadow = settingsSeparatorShadow;
            settingsSeparatorContainer.Translation = new System.Numerics.Vector3(0, 0, 2);
            mainStack.Children.Add(settingsSeparatorContainer);

            var settingsButton = CreateIconButton("\uE713", "Toolbar Settings", (s, e) =>
            {
                var win = new SettingsWindow();
                win.AppWindow.Move(new Windows.Graphics.PointInt32(this.AppWindow.Position.X + 50, this.AppWindow.Position.Y + 60));
                win.Activate();
            });
            mainStack.Children.Add(settingsButton);
        }

        private FrameworkElement CreateIconButton(string content, string tooltip, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = new FontIcon { Glyph = content, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16 },
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)), // Transparent base
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                UseLayoutRounding = true,
            };

            // Create text label for button
            var textLabel = new TextBlock
            {
                Text = "Settings",
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 50,
                Margin = new Thickness(0, 2, 0, 0),
                UseLayoutRounding = true,
            };

            // Create container stack panel for button + text
            var containerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 0,
                Width = 54, // Slightly wider to accommodate text
                Margin = new Thickness(2),
            };

            // Use WinUI button visual state resources to ensure stable hover/pressed visuals
            var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0));
            var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(100, 0, 0, 0));
            var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            // Override per-button theme resources so the default template keeps our visuals
            button.Resources["ButtonBackground"] = normalBrush;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = normalBrush;

            // Add button and text to the container stack
            containerStack.Children.Add(button);
            containerStack.Children.Add(textLabel);

            ToolTipService.SetToolTip(button, tooltip);
            button.Click += clickHandler;
            return containerStack;
        }

        private FrameworkElement CreateIconButton(ButtonGroup group, ToolbarButton model)
        {
            var dispatcher = DispatcherQueue;

            var button = new Button
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                UseLayoutRounding = true,
            };

            // Create text label for button with smaller font and fixed positioning
            var textLabel = new TextBlock
            {
                Text = model.Name ?? string.Empty,
                FontSize = 7,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 48,
                Height = 12,
                Margin = new Thickness(0, 2, 0, 0),
                UseLayoutRounding = true,
            };

            // Create container stack panel for button + text with fixed dimensions
            var containerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 0,
                Width = 50,
                Height = 48,
                Margin = new Thickness(2),
            };

            var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0));
            var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(100, 0, 0, 0));
            var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackground"] = normalBrush;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = normalBrush;

            FrameworkElement BuildIcon()
            {
                if (model.IconType == ToolbarIconType.Image && !string.IsNullOrWhiteSpace(model.IconPath))
                {
                    try
                    {
                        Uri imgUri = null;
                        var rawPath = model.IconPath;
                        if (File.Exists(rawPath))
                        {
                            var ver = File.GetLastWriteTimeUtc(rawPath).Ticks;
                            var ub = new UriBuilder
                            {
                                Scheme = "file",
                                Path = rawPath,
                                Query = $"v={ver}",
                            };
                            imgUri = ub.Uri;
                        }
                        else if (Uri.TryCreate(rawPath, UriKind.Absolute, out var parsed))
                        {
                            imgUri = parsed;
                        }
                        else
                        {
                            var prefixed = new UriBuilder { Scheme = "file", Path = rawPath }.Uri;
                            imgUri = prefixed;
                        }

                        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        bmp.UriSource = imgUri;
                        return new Image
                        {
                            Width = 16,
                            Height = 16,
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                            Source = bmp,
                        };
                    }
                    catch
                    {
                    }
                }

                return new FontIcon
                {
                    Glyph = model.IconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                };
            }

            var iconElement = BuildIcon();
            var contentGrid = new Grid
            {
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            contentGrid.Children.Add(iconElement);

            var progressRing = new ProgressRing
            {
                Width = 20,
                Height = 20,
                IsActive = false,
                Visibility = Visibility.Collapsed,
            };
            contentGrid.Children.Add(progressRing);

            button.Content = contentGrid;

            // Add button and text to the container stack
            containerStack.Children.Add(button);
            containerStack.Children.Add(textLabel);

            void UpdateVisualState()
            {
                void ApplyState()
                {
                    button.IsEnabled = model.IsEnabled && !model.IsExecuting;
                    progressRing.IsActive = model.IsExecuting;
                    progressRing.Visibility = model.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
                    iconElement.Opacity = model.IsExecuting ? 0.4 : 1.0;
                }

                if (dispatcher.HasThreadAccess)
                {
                    ApplyState();
                }
                else
                {
                    dispatcher.TryEnqueue(ApplyState);
                }
            }

            string BuildTooltip()
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(model.Name))
                {
                    parts.Add(model.Name);
                }

                if (!string.IsNullOrWhiteSpace(model.Description))
                {
                    parts.Add(model.Description);
                }

                if (!string.IsNullOrWhiteSpace(model.ProgressMessage))
                {
                    parts.Add(model.ProgressMessage);
                }
                else if (!string.IsNullOrWhiteSpace(model.StatusMessage))
                {
                    parts.Add(model.StatusMessage);
                }

                if (parts.Count == 0)
                {
                    return model.Name ?? string.Empty;
                }

                return string.Join(Environment.NewLine, parts);
            }

            void UpdateToolTip()
            {
                var tooltip = BuildTooltip();
                void Apply()
                {
                    Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, tooltip);
                }

                if (dispatcher.HasThreadAccess)
                {
                    Apply();
                }
                else
                {
                    dispatcher.TryEnqueue(Apply);
                }
            }

            async void OnClick(object sender, RoutedEventArgs e)
            {
                if (model.IsExecuting)
                {
                    return;
                }

                try
                {
                    await _actionExecutor.ExecuteAsync(group, model);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    model.StatusMessage = ex.Message;
                }
            }

            UpdateVisualState();
            UpdateToolTip();

            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ToolbarButton.IsEnabled) ||
                    e.PropertyName == nameof(ToolbarButton.IsExecuting))
                {
                    UpdateVisualState();
                }
                else if (e.PropertyName == nameof(ToolbarButton.ProgressMessage) ||
                         e.PropertyName == nameof(ToolbarButton.StatusMessage) ||
                         e.PropertyName == nameof(ToolbarButton.Description) ||
                         e.PropertyName == nameof(ToolbarButton.Name))
                {
                    UpdateToolTip();

                    // Update text label when name changes
                    if (e.PropertyName == nameof(ToolbarButton.Name))
                    {
                        if (dispatcher.HasThreadAccess)
                        {
                            textLabel.Text = model.Name ?? string.Empty;
                        }
                        else
                        {
                            dispatcher.TryEnqueue(() => textLabel.Text = model.Name ?? string.Empty);
                        }
                    }
                }
            };

            button.Click += OnClick;
            return containerStack;
        }

        // Build or replace a single group's visual container in-place; falls back to full rebuild if structure missing.
        private void BuildOrReplaceSingleGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            StackPanel mainStack = (_toolbarContainer?.Child as ScrollViewer)?.Content as StackPanel
                                   ?? _toolbarContainer?.Child as StackPanel;
            if (mainStack == null)
            {
                return;
            }

            var group = _store.Groups.FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                // Removed group: trigger full rebuild to also clean separators coherently.
                BuildToolbarFromStore();
                ResizeToContent();
                return;
            }

            // Locate existing group container (Border) tagged with group id.
            Border existingContainer = null;
            for (int i = 0; i < mainStack.Children.Count; i++)
            {
                if (mainStack.Children[i] is Border b && b.Tag is string tag && string.Equals(tag, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    existingContainer = b;
                    break;
                }
            }

            var enabledButtons = group.Buttons.Where(b => b.IsEnabled).ToList();
            if (enabledButtons.Count == 0)
            {
                // If no buttons remain, treat as removal
                BuildToolbarFromStore();
                ResizeToContent();
                return;
            }

            Border newContainer = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 0, 0)),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2, 0, 2, 0),
                Tag = group.Id,
            };
            var groupShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
            newContainer.Shadow = groupShadow;
            newContainer.Translation = new System.Numerics.Vector3(0, 0, 1);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            foreach (var btn in enabledButtons)
            {
                panel.Children.Add(CreateIconButton(group, btn));
            }

            newContainer.Child = panel;

            if (existingContainer == null)
            {
                // Find settings separator anchor
                int anchorIndex = -1;
                for (int i = 0; i < mainStack.Children.Count; i++)
                {
                    if (mainStack.Children[i] is Border b && b.Tag as string == "__SETTINGS_SEPARATOR__")
                    {
                        anchorIndex = i;
                        break;
                    }
                }

                int insertIndex = anchorIndex >= 0 ? anchorIndex : mainStack.Children.Count;
                mainStack.Children.Insert(insertIndex, newContainer);
            }
            else
            {
                int idx = mainStack.Children.IndexOf(existingContainer);
                if (idx >= 0)
                {
                    mainStack.Children.RemoveAt(idx);
                    mainStack.Children.Insert(idx, newContainer);
                }
                else
                {
                    mainStack.Children.Add(newContainer);
                }
            }

            ResizeToContent();
        }

        private void ResizeToContent()
        {
            if (_toolbarContainer != null)
            {
                // Measure content desired width independent of current constraints
                StackPanel mainStack = (_toolbarContainer.Child as ScrollViewer)?.Content as StackPanel
                                       ?? _toolbarContainer.Child as StackPanel;
                if (mainStack == null)
                {
                    return;
                }

                mainStack.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                double desiredWidth = mainStack.DesiredSize.Width + _toolbarContainer.Padding.Left + _toolbarContainer.Padding.Right;
                double desiredHeight = _toolbarContainer.ActualHeight > 0 ? _toolbarContainer.ActualHeight : 75;

                var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
                double maxWidth = displayArea.WorkArea.Width / 2.0;
                double widthToSet = Math.Min(desiredWidth, maxWidth);

                // Add symmetric space for shadow
                int shadowPadding = 12; // Symmetric padding on all sides
                int width = (int)Math.Ceiling(widthToSet) + (shadowPadding * 2);
                int height = (int)Math.Ceiling(desiredHeight) + (shadowPadding * 2);

                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            }
        }

        private void PositionAtTopCenter()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            int width = this.AppWindow.Size.Width;
            int height = this.AppWindow.Size.Height;
            int x = workArea.X + ((workArea.Width - width) / 2);
            int y = workArea.Y - height; // hidden above top
            this.AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        private void StartMonitoring()
        {
            _monitorTimer = new Timer(120);
            _monitorTimer.Elapsed += MonitorTimer_Elapsed;
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();
        }

        private void MonitorTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            GetCursorPos(out var pt);
            var displayArea = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            var topEdge = displayArea.WorkArea.Y;
            bool inTrigger = pt.Y <= topEdge + TriggerZoneHeight;

            if (inTrigger && !_isVisible)
            {
                DispatcherQueue.TryEnqueue(() => ShowToolbar());
            }
            else if (!inTrigger && _isVisible)
            {
                // hide when cursor is not over the toolbar rectangle
                DispatcherQueue.TryEnqueue(() =>
                {
                    var winPos = this.AppWindow.Position;
                    var winSize = this.AppWindow.Size;
                    bool overToolbar = pt.X >= winPos.X && pt.X <= winPos.X + winSize.Width &&
                                       pt.Y >= winPos.Y && pt.Y <= winPos.Y + winSize.Height;
                    if (!overToolbar)
                    {
                        HideToolbar();
                    }
                });
            }
        }

        private void ShowToolbar()
        {
            _isVisible = true;

            // Reposition to current monitor top edge
            GetCursorPos(out var pt);
            var da = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            var work = da.WorkArea;
            var size = AppWindow.Size;
            int x = work.X + ((work.Width - size.Width) / 2);
            int y = work.Y; // flush with top

            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
            AppWindow.Show(false); // show without activation
            MakeTopMost();
        }

        private void HideToolbar(bool initial = false)
        {
            _isVisible = false;
            AppWindow.Hide();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // keep always on top
            MakeTopMost();
        }

        // Incremental diff update for workspace group to avoid full rebuild.
        // Obsolete: old incremental workspace diff path (replaced by store). Retained temporarily for reference.
        private void ReplaceOrInsertWorkspaceGroup(ButtonGroup newGroup, ProviderChangedEventArgs changeArgs = null)
        {
            // Intentionally left empty (legacy path). Will be removed after confirming store path stability.
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // P/Invoke to keep window topmost if WinUIEx helper not available
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;

        private void MakeTopMost()
        {
            var handle = _hwnd != IntPtr.Zero ? _hwnd : this.GetWindowHandle();
            SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }

        private void ApplyFramelessStyles()
        {
            // Remove caption / border styles so only the toolbar content is visible
            const int GWL_STYLE = -16;
            const int GWL_EXSTYLE = -20;
            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_SYSMENU = 0x00080000;
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_VISIBLE = 0x10000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST = 0x00000008;

            var h = _hwnd;
            int style = GetWindowLong(h, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
            style |= WS_POPUP | WS_VISIBLE;
            _ = SetWindowLong(h, GWL_STYLE, style);

            int exStyle = GetWindowLong(h, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            _ = SetWindowLong(h, GWL_EXSTYLE, exStyle);
        }

        private void ApplyTransparentBackground()
        {
            // The key is to NOT have any background on the window itself
            // With WS_EX_LAYERED + no background, only visible content shows
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        public void Dispose()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            _configWatcherDebounce?.Stop();
            _configWatcherDebounce?.Dispose();
            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Dispose();
            }

            foreach (var disposable in _providerDisposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception)
                {
                }
            }

            _providerDisposables.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
