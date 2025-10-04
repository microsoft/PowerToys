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
using TopToolbar.Controls;
using TopToolbar.Helpers;
using TopToolbar.Models;
using TopToolbar.Providers;
using TopToolbar.Providers.Configuration;
using TopToolbar.Providers.External.Mcp;
using TopToolbar.Services;
using TopToolbar.Services.Workspaces;
using TopToolbar.ViewModels;
using Windows.UI;
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
        private readonly BuiltinProvider _builtinProvider;
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
        private IntPtr _oldWndProc;
        private DpiWndProcDelegate _newWndProc;

        // Profile runtime abstraction (replaces direct file service handling in this window)
        private Services.Profiles.IProfileRuntime _profileRuntime;
        private int _profileRebuildInFlight;
        private bool _snapshotInProgress;

        private delegate IntPtr DpiWndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public ToolbarWindow()
        {
            _configService = new ToolbarConfigService();
            _contextFactory = new ActionContextFactory();
            _providerRuntime = new ActionProviderRuntime();
            _providerService = new ActionProviderService(_providerRuntime);
            _actionExecutor = new ToolbarActionExecutor(_providerService, _contextFactory);
            _builtinProvider = new BuiltinProvider();
            _vm = new ToolbarViewModel(_configService, _providerService, _contextFactory);
            EnsurePerMonitorV2();
            RegisterProviders();

            // TODO (Profiles): Inject ProfileManager & EffectiveModelBuilder here.
            // 1. Load registry + active profile.
            // 2. Load provider definition catalog.
            // 3. Build effective model and drive initial group/button creation instead of raw config.
            // 4. Subscribe to ActiveProfileChanged / OverridesChanged to rebuild (diff-based) UI.

            // Legacy _store.StoreChanged full rebuild removed; we now rely solely on detailed events.
            _store.StoreChangedDetailed += (s, e) =>
            {
                try
                {
                    if (e == null || e.Kind == TopToolbar.Stores.StoreChangeKind.Reset || string.IsNullOrWhiteSpace(e.GroupId))
                    {
                        // Rehook all groups (in case of reset) then rebuild
                        HookAllGroupsForEnabledChanges();
                        BuildToolbarFromStore();
                        ResizeToContent();
                        return;
                    }

                    // Attempt partial update for single group
                    if (!DispatcherQueue.TryEnqueue(() =>
                    {
                        BuildOrReplaceSingleGroup(e.GroupId);
                        _lastPartialUpdateTick = Environment.TickCount;

                        // Ensure hook exists for updated group id
                        HookGroupForEnabledChanges(e.GroupId);
                    }))
                    {
                        BuildOrReplaceSingleGroup(e.GroupId);
                        _lastPartialUpdateTick = Environment.TickCount;
                        HookGroupForEnabledChanges(e.GroupId);
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
                    TryHookDpiMessages();
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

                // Wait for profile runtime to initialize before building UI
                await _profileRuntime.InitializeAsync();

                // Ensure UI-thread access for XAML object tree
                DispatcherQueue.TryEnqueue(() =>
                {
                    SyncStaticGroupsIntoStore();
                    BuildToolbarFromStore();
                    HookAllGroupsForEnabledChanges();
                    ResizeToContent();
                    PositionAtTopCenter();
                    _builtConfigOnce = true;
                });
            };

            // Initialize profile runtime (moved initialization here to ensure it's ready for await above)
            _profileRuntime = new Services.Profiles.ProfileRuntime();
            _profileRuntime.ActiveProfileChanged += (s, p) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"ToolbarWindow.ActiveProfileChanged: Profile changed to {p?.Name ?? "null"}");
                    SafeEnqueueProfileRebuild();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ToolbarWindow.ActiveProfileChanged: Error handling profile change: {ex.Message}");
                }
            };
        }

        public void SwitchProfile(string profileId)
        {
            try
            {
                _profileRuntime?.Switch(profileId);
            }
            catch
            {
            }
        }

        public IReadOnlyList<TopToolbar.Models.Abstractions.IProfile> GetAllProfiles()
        {
            try
            {
                return _profileRuntime?.GetAllProfiles() ?? new List<TopToolbar.Models.Abstractions.IProfile>();
            }
            catch
            {
                return new List<TopToolbar.Models.Abstractions.IProfile>();
            }
        }

        private void SafeEnqueueProfileRebuild()
        {
            if (_profileRuntime == null)
            {
                return;
            }

            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (System.Threading.Interlocked.CompareExchange(ref _profileRebuildInFlight, 1, 0) != 0)
                    {
                        return;
                    }

                    try
                    {
                        RebuildFromActiveProfile();
                    }
                    catch (Exception rex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TopToolbar: Profile rebuild failed: {rex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _profileRebuildInFlight, 0);
                    }
                });
            }
            catch
            {
            }
        }

        private void RebuildFromActiveProfile()
        {
            var activeProfile = _profileRuntime?.ActiveProfile;
            if (_profileRuntime == null || activeProfile == null)
            {
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"TopToolbar: Building UI for profile: {activeProfile.Name}");

                // Instead of only using profile groups, we need to rebuild the entire store
                // which includes both static config groups and dynamic provider groups,
                // then apply profile filtering when building the UI

                // First, ensure static groups from config are synced into store
                SyncStaticGroupsIntoStore();

                void ApplyUI()
                {
                    try
                    {
                        // Use the existing method that applies profile filtering
                        BuildToolbarFromStore();
                        ResizeToContent();
                        System.Diagnostics.Debug.WriteLine($"TopToolbar: UI rebuilt with profile filtering applied");
                    }
                    catch (Exception apEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"TopToolbar: Apply UI failed: {apEx.Message}");
                    }
                }

                if (DispatcherQueue.HasThreadAccess)
                {
                    ApplyUI();
                }
                else
                {
                    DispatcherQueue.TryEnqueue(ApplyUI);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TopToolbar: Profile rebuild failed: {ex.Message}");
            }
        }

        // Track which groups we've already hooked to avoid duplicate handlers
        private readonly HashSet<string> _enabledChangeHooked = new(StringComparer.OrdinalIgnoreCase);

        private void HookAllGroupsForEnabledChanges()
        {
            foreach (var g in _store.Groups)
            {
                if (g != null)
                {
                    HookGroupForEnabledChanges(g.Id);
                }
            }
        }

        private void HookGroupForEnabledChanges(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            var group = _store.GetGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (_enabledChangeHooked.Contains(group.Id))
            {
                return; // already hooked
            }

            group.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ButtonGroup.IsEnabled))
                {
                    try
                    {
                        if (!DispatcherQueue.TryEnqueue(() =>
                        {
                            BuildToolbarFromStore();
                            ResizeToContent();
                        }))
                        {
                            BuildToolbarFromStore();
                            ResizeToContent();
                        }
                    }
                    catch
                    {
                    }
                }
            };
            _enabledChangeHooked.Add(group.Id);
        }

        private void RegisterProviders()
        {
            try
            {
                // Initialize and register all built-in providers (workspace and MCP providers)
                _builtinProvider.Initialize();
                _builtinProvider.RegisterProvidersTo(_providerRuntime);
            }
            catch (Exception ex)
            {
                // Log error but continue
                try
                {
                    System.Diagnostics.Debug.WriteLine($"ToolbarWindow: Failed to register built-in providers: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }
            }
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

                // Updated per user request: light semi-transparent gray
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(0xCC, 0xF0, 0xF0, 0xF0)), // #CCF0F0F0 (~80% opacity light gray)
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

        // Helper methods to respect profile settings when building from store
        private bool IsGroupEnabledInProfile(ButtonGroup group)
        {
            var activeProfile = _profileRuntime?.ActiveProfile;
            if (activeProfile == null)
            {
                return true; // If no profile, respect store enabled state only
            }

            // Check if this group exists in profile and is enabled
            var profileGroup = activeProfile.Groups?.FirstOrDefault(pg =>
                string.Equals(pg.Id, group.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pg.Name, group.Name, StringComparison.OrdinalIgnoreCase));

            // If group not in profile, default to enabled; otherwise use profile setting
            return profileGroup?.IsEnabled ?? true;
        }

        private bool IsActionEnabledInProfile(ButtonGroup group, ToolbarButton button)
        {
            var activeProfile = _profileRuntime?.ActiveProfile;
            if (activeProfile == null)
            {
                return true; // If no profile, respect store enabled state only
            }

            // Find the corresponding profile group
            var profileGroup = activeProfile.Groups?.FirstOrDefault(pg =>
                string.Equals(pg.Id, group.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pg.Name, group.Name, StringComparison.OrdinalIgnoreCase));

            if (profileGroup == null)
            {
                return true; // Group not in profile, default to enabled
            }

            // Check if the action exists in profile group and is enabled
            var profileAction = profileGroup.Actions?.FirstOrDefault(pa =>
                string.Equals(pa.Id, button.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pa.Name, button.Name, StringComparison.OrdinalIgnoreCase));

            // If action not in profile group, default to enabled; otherwise use profile setting
            return profileAction?.IsEnabled ?? true;
        }

        // Transitional full rebuild method (will be replaced by ItemsRepeater binding in Phase 2)
        private void BuildToolbarFromStore()
        {
            StackPanel mainStack = (_toolbarContainer?.Child as ScrollViewer)?.Content as StackPanel
                                   ?? _toolbarContainer?.Child as StackPanel;
            if (mainStack == null)
            {
                return;
            }

            mainStack.Children.Clear();

            // Add profile UI as first element
            var activeProfile = _profileRuntime?.ActiveProfile;
            if (_profileRuntime != null && activeProfile != null)
            {
                // Add profile display button
                var profileDisplayName = activeProfile.Name ?? _profileRuntime.ActiveProfileId;

                var profileButton = CreateIconButton(
                    "\uE77B",
                    $"Current Profile: {profileDisplayName}",
                    (s, e) =>
                    {
                        // Create and show profile switcher menu
                        ShowProfileSwitcherMenu(s as FrameworkElement);
                    },
                    profileDisplayName);
                mainStack.Children.Add(profileButton);
            }

            // Filter enabled groups and buttons, respecting profile settings if available
            var activeGroups = _store.Groups
                .Where(g => g != null && g.IsEnabled && IsGroupEnabledInProfile(g))
                .Select(g => new { Group = g, EnabledButtons = g.Buttons.Where(b => b.IsEnabled && IsActionEnabledInProfile(g, b)).ToList() })
                .Where(x => x.EnabledButtons.Count > 0)
                .ToList();

            for (int gi = 0; gi < activeGroups.Count; gi++)
            {
                var group = activeGroups[gi].Group;
                var enabledButtons = activeGroups[gi].EnabledButtons;

                var groupContainer = new Border
                {
                    CornerRadius = new CornerRadius(15),

                    // Default fully transparent so it doesn't show until hover
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(2, 0, 2, 0),
                    Tag = group.Id,
                };

                var groupShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
                groupContainer.Shadow = groupShadow;
                groupContainer.Translation = new System.Numerics.Vector3(0, 0, 1);

                // Group hover background highlight (keeps button animations intact)
                groupContainer.PointerEntered += (s, e) =>
                {
                    try
                    {
                        if (groupContainer.Background is Microsoft.UI.Xaml.Media.SolidColorBrush b)
                        {
                            // Light gray hover background (slightly translucent)
                            b.Color = Windows.UI.Color.FromArgb(40, 0, 0, 0); // alpha 40 ~ 16% dark overlay
                        }
                        else
                        {
                            groupContainer.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0));
                        }
                    }
                    catch
                    {
                    }
                };
                groupContainer.PointerExited += (s, e) =>
                {
                    try
                    {
                        if (groupContainer.Background is Microsoft.UI.Xaml.Media.SolidColorBrush b)
                        {
                            b.Color = Windows.UI.Color.FromArgb(0, 0, 0, 0); // back to transparent
                        }
                        else
                        {
                            groupContainer.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                        }
                    }
                    catch
                    {
                    }
                };

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

            var snapshotButton = CreateIconButton("\uE114", "Snapshot workspace", async (s, e) => await HandleSnapshotButtonClickAsync(s as Button), "Snapshot");
            mainStack.Children.Add(snapshotButton);

            var settingsButton = CreateIconButton("\uE713", "Toolbar Settings", (s, e) =>
            {
                var win = new SettingsWindow(_profileRuntime, null);
                win.AppWindow.Move(new Windows.Graphics.PointInt32(this.AppWindow.Position.X + 50, this.AppWindow.Position.Y + 60));
                win.Activate();
            });
            mainStack.Children.Add(settingsButton);
        }

        private async System.Threading.Tasks.Task HandleSnapshotButtonClickAsync(Button triggerButton)
        {
            if (_snapshotInProgress)
            {
                return;
            }

            if (this.Content is not FrameworkElement rootElement || rootElement.XamlRoot is null)
            {
                return;
            }

            _snapshotInProgress = true;
            if (triggerButton != null)
            {
                triggerButton.IsEnabled = false;
            }

            try
            {
                var workspaceName = await SnapshotPromptWindow.ShowAsync(this).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(workspaceName))
                {
                    return;
                }

                try
                {
                    using var runtime = new WorkspacesRuntimeService();
                    var workspace = await runtime.SnapshotAsync(workspaceName, CancellationToken.None).ConfigureAwait(true);
                    if (workspace == null)
                    {
                        await ShowSimpleMessageAsync(rootElement.XamlRoot, "Snapshot failed", "No eligible windows were detected to capture.");
                        return;
                    }

                    await ShowSimpleMessageAsync(rootElement.XamlRoot, "Snapshot saved", $"Workspace \"{workspace.Name}\" has been saved.");
                    await RefreshWorkspaceGroupAsync();
                }
                catch (Exception ex)
                {
                    await ShowSimpleMessageAsync(rootElement.XamlRoot, "Snapshot failed", ex.Message);
                }
            }
            finally
            {
                if (triggerButton != null)
                {
                    triggerButton.IsEnabled = true;
                }

                _snapshotInProgress = false;
            }
        }

        private static async System.Threading.Tasks.Task ShowSimpleMessageAsync(XamlRoot xamlRoot, string title, string message)
        {
            if (xamlRoot == null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title ?? string.Empty,
                Content = new TextBlock
                {
                    Text = message ?? string.Empty,
                    TextWrapping = TextWrapping.Wrap,
                },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
            };

            await dialog.ShowAsync();
        }

        private async System.Threading.Tasks.Task RefreshWorkspaceGroupAsync()
        {
            try
            {
                var context = new ActionContext();
                var group = await _providerService.CreateGroupAsync("WorkspaceProvider", context, CancellationToken.None).ConfigureAwait(true);
                if (group == null)
                {
                    return;
                }

                void Apply()
                {
                    try
                    {
                        _store.UpsertProviderGroup(group);
                    }
                    catch
                    {
                    }
                }

                if (!DispatcherQueue.TryEnqueue(Apply))
                {
                    Apply();
                }
            }
            catch
            {
            }
        }

        private FrameworkElement CreateIconButton(string content, string tooltip, RoutedEventHandler clickHandler, string labelText = "Settings")
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
                Text = labelText,
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
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

            const double buttonSize = 32d;
            const double iconSize = 16d;
            const double containerWidth = 54d;
            const double maxLabelWidth = 50d;
            const double progressRingSize = 16d;

            var button = new Button
            {
                Width = buttonSize,
                Height = buttonSize,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
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

            var textLabel = new TextBlock
            {
                Text = model.Name ?? string.Empty,
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 100, 100, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = maxLabelWidth,
                Margin = new Thickness(0, 2, 0, 0),
                UseLayoutRounding = true,
            };

            var containerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2,
                Width = containerWidth,
                Margin = new Thickness(2),
            };

            var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackground"] = normalBrush;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = normalBrush;

            var iconPresenter = new Controls.ToolbarIconPresenter
            {
                IconSize = iconSize,
                Foreground = Color.FromArgb(255, 255, 255, 255),
                Button = model,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var iconHost = new Grid
            {
                Width = buttonSize,
                Height = buttonSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var progressRing = new ProgressRing
            {
                Width = progressRingSize,
                Height = progressRingSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsActive = false,
                Visibility = Visibility.Collapsed,
            };
            iconHost.Children.Add(iconPresenter);
            iconHost.Children.Add(progressRing);

            button.Content = iconHost;

            containerStack.Children.Add(button);
            containerStack.Children.Add(textLabel);

            void UpdateVisualState()
            {
                void ApplyState()
                {
                    button.IsEnabled = model.IsEnabled && !model.IsExecuting;
                    progressRing.IsActive = model.IsExecuting;
                    progressRing.Visibility = model.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
                    if (iconPresenter != null)
                    {
                        iconPresenter.Opacity = model.IsExecuting ? 0.4 : 1.0;
                    }
                }

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(ApplyState);
                }
                else
                {
                    ApplyState();
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

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    dispatcher.TryEnqueue(Apply);
                }
                else
                {
                    Apply();
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
                if (e == null)
                {
                    UpdateVisualState();
                    UpdateToolTip();
                    return;
                }

                if (e.PropertyName == nameof(ToolbarButton.IsEnabled) ||
                    e.PropertyName == nameof(ToolbarButton.IsExecuting))
                {
                    UpdateVisualState();
                }

                if (e.PropertyName == nameof(ToolbarButton.ProgressMessage) ||
                    e.PropertyName == nameof(ToolbarButton.StatusMessage) ||
                    e.PropertyName == nameof(ToolbarButton.Description) ||
                    e.PropertyName == nameof(ToolbarButton.Name) ||
                    e.PropertyName == nameof(ToolbarButton.IconType) ||
                    e.PropertyName == nameof(ToolbarButton.IconPath) ||
                    e.PropertyName == nameof(ToolbarButton.IconGlyph))
                {
                    UpdateToolTip();
                }

                if (e.PropertyName == nameof(ToolbarButton.Name))
                {
                    void Apply()
                    {
                        textLabel.Text = model.Name ?? string.Empty;
                    }

                    if (dispatcher != null && !dispatcher.HasThreadAccess)
                    {
                        dispatcher.TryEnqueue(Apply);
                    }
                    else
                    {
                        Apply();
                    }
                }
            };
            button.Click += OnClick;
            return containerStack;
        }

        // Build or replace a single group's visual container in-place; falls back to full rebuild if structure missing.
        // Transitional incremental update path prior to full data binding migration
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
            if (group == null || !group.IsEnabled || !IsGroupEnabledInProfile(group))
            {
                // Removed or disabled group (either in store or profile): trigger full rebuild to also clean separators coherently.
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

            var enabledButtons = group.Buttons.Where(b => b.IsEnabled && IsActionEnabledInProfile(group, b)).ToList();
            if (enabledButtons.Count == 0)
            {
                // If no buttons remain (after both store and profile filtering), treat as removal
                BuildToolbarFromStore();
                ResizeToContent();
                return;
            }

            Border newContainer = new Border
            {
                CornerRadius = new CornerRadius(15),

                // Default transparent; hover will apply highlight
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2, 0, 2, 0),
                Tag = group.Id,
            };
            var groupShadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
            newContainer.Shadow = groupShadow;
            newContainer.Translation = new System.Numerics.Vector3(0, 0, 1);

            // Group hover background highlight (incremental path)
            newContainer.PointerEntered += (s, e) =>
            {
                try
                {
                    if (newContainer.Background is Microsoft.UI.Xaml.Media.SolidColorBrush b)
                    {
                        b.Color = Windows.UI.Color.FromArgb(40, 0, 0, 0);
                    }
                    else
                    {
                        newContainer.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0));
                    }
                }
                catch
                {
                }
            };
            newContainer.PointerExited += (s, e) =>
            {
                try
                {
                    if (newContainer.Background is Microsoft.UI.Xaml.Media.SolidColorBrush b)
                    {
                        b.Color = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    }
                    else
                    {
                        newContainer.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                    }
                }
                catch
                {
                }
            };

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

            // Work area coordinates are already in effective (DIP) on WinUI 3; keep logic but centralize for clarity
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
            GetCursorPos(out var ptPx);
            var da = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(ptPx.X, ptPx.Y), DisplayAreaFallback.Primary);
            var work = da.WorkArea;
            var size = AppWindow.Size;
            int x = work.X + ((work.Width - size.Width) / 2);
            int y = work.Y; // flush with top
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
            AppWindow.Show(false); // show without activation
            MakeTopMost();
        }

        // Helper: convert raw pixel to DIP relative to window DPI (if needed later for precise placement)
        private double PxToDip(int px)
        {
            var dpi = GetDpiForWindow(_hwnd != IntPtr.Zero ? _hwnd : this.GetWindowHandle());
            return (double)px * 96.0 / dpi;
        }

        private void EnsurePerMonitorV2()
        {
            try
            {
                // Attempt to set per-monitor V2 awareness at runtime (harmless if already set via manifest)
                SetProcessDpiAwarenessContext(new IntPtr(-4)); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 constant
            }
            catch
            {
            }
        }

        private void TryHookDpiMessages()
        {
            try
            {
                if (_hwnd == IntPtr.Zero)
                {
                    return;
                }

                _newWndProc = DpiWndProc; // keep delegate alive
                _oldWndProc = SetWindowLongPtr(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc)); // GWL_WNDPROC = -4
            }
            catch
            {
            }
        }

        private IntPtr DpiWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            const int WM_DPICHANGED = 0x02E0;
            if (msg == WM_DPICHANGED)
            {
                // lParam points to a RECT in new DPI suggested size/pos
                try
                {
                    BuildToolbarFromStore();
                    ResizeToContent();
                }
                catch
                {
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

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

        private void ShowProfileSwitcherMenu(FrameworkElement targetElement)
        {
            if (_profileRuntime == null || targetElement == null)
            {
                return;
            }

            try
            {
                var profiles = GetAllProfiles();
                var activeProfileId = _profileRuntime.ActiveProfileId;

                // Create menu flyout
                var menuFlyout = new MenuFlyout();

                foreach (var profile in profiles)
                {
                    var menuItem = new MenuFlyoutItem
                    {
                        Text = profile.Name,
                        Tag = profile.Id,
                    };

                    // Mark current profile with checkmark
                    if (string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase))
                    {
                        menuItem.Icon = new FontIcon { Glyph = "\uE73E" }; // Checkmark
                    }

                    menuItem.Click += (s, e) =>
                    {
                        var clickedProfileId = (s as MenuFlyoutItem)?.Tag as string;
                        if (!string.IsNullOrWhiteSpace(clickedProfileId) &&
                            !string.Equals(clickedProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase))
                        {
                            SwitchProfile(clickedProfileId);
                        }
                    };

                    menuFlyout.Items.Add(menuItem);
                }

                // Show the menu at the target element
                menuFlyout.ShowAt(targetElement);
            }
            catch
            {
                // Silently handle errors for now
            }
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

            // Dispose the built-in provider which handles all provider disposals
            try
            {
                _builtinProvider?.Dispose();
            }
            catch (Exception)
            {
            }

            GC.SuppressFinalize(this);
        }
    }
}
