// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.WinUI.Controls;
using System.Threading.Tasks;
using Microsoft.UI; // Colors namespace
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input; // KeyRoutedEventArgs
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media; // VisualTreeHelper
using Microsoft.UI.Xaml.Media.Imaging;
using TopToolbar.Models;
using TopToolbar.Services;
using TopToolbar.Providers;
using TopToolbar.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TopToolbar
{
    public sealed partial class SettingsWindow : WinUIEx.WindowEx, IDisposable
    {
        private readonly SettingsViewModel _vm;
        private readonly Services.Profiles.IProfileManager _profileManager;
        private readonly Services.Profiles.ProfileFileService _profileFileService;
        private readonly Services.Profiles.IProfileRuntime _profileRuntime;
        private readonly bool _ownsFileService;

        private bool _isClosed;
        private bool _disposed;
        private FrameworkElement _appTitleBarCache;

        private Models.Profile _selectedProfile;
        private System.Collections.Generic.List<Models.ProfileGroup> _currentProfileGroups = new();

        public SettingsViewModel ViewModel => _vm;

        public Models.Profile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SafeLogWarning($"Setting SelectedProfile to: {value?.Name ?? "null"} with {value?.Groups?.Count ?? 0} groups");
                _selectedProfile = value;
                UpdateProfileUI();
                if (_selectedProfile != null)
                {
                    // Reset legacy ViewModel group selection so old groups/buttons UI disappears
                    try
                    {
                        if (_vm.SelectedGroup != null)
                        {
                            _vm.SelectedGroup = null; // This should toggle HasSelectedGroup/HasNoSelectedGroup
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeLogWarning($"Failed to reset legacy SelectedGroup: {ex.Message}");
                    }

                    _currentProfileGroups = _selectedProfile.Groups ?? new System.Collections.Generic.List<Models.ProfileGroup>();
                    SafeLogWarning($"Updated _currentProfileGroups to have {_currentProfileGroups.Count} groups");
                    UpdateActionsUI();
                }
                else
                {
                    _currentProfileGroups.Clear();
                    SafeLogWarning("Cleared _currentProfileGroups");
                    try
                    {
                        if (_vm.SelectedGroup != null)
                        {
                            _vm.SelectedGroup = null;
                        }
                    }
                    catch
                    {
                        // Intentionally ignored: legacy VM cleanup best-effort
                    }
                }
            }
        }

        /// <summary>
        /// Explicitly clears legacy group editing UI so that when switching to a profile with no groups
        /// the right side does not continue to show the previous profile's group/buttons.
        /// </summary>
        private void ResetLegacyGroupEditingUI()
        {
            try
            {
                if (_vm.SelectedGroup != null)
                {
                    _vm.SelectedGroup = null;
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"ResetLegacyGroupEditingUI failed: {ex.Message}");
            }
        }

        // New preferred overload: accept runtime (unified state). Falls back to file service if null.
        public SettingsWindow(Services.Profiles.IProfileRuntime profileRuntime, Services.Profiles.IProfileManager profileManager = null)
        {
            try
            {
                // Use reflection to invoke generated InitializeComponent to satisfy editor analysis without defining a duplicate stub.
                var init = typeof(SettingsWindow).GetMethod("InitializeComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                init?.Invoke(this, null);
            }
            catch (Exception ex)
            {
                SafeLogWarning("InitializeComponent fallback: " + ex.Message);
            }

            _profileRuntime = profileRuntime;
            _profileManager = profileManager;
            if (_profileRuntime != null)
            {
                _profileFileService = _profileRuntime.FileService;
                _ownsFileService = false; // runtime manages lifecycle
            }
            else
            {
                _profileFileService = new Services.Profiles.ProfileFileService();
                _ownsFileService = true;
            }

            _vm = new SettingsViewModel(new ToolbarConfigService());
            InitializeWindowStyling();
            this.Closed += async (s, e) =>
            {
                await _vm.SaveAsync();
                if (_ownsFileService)
                {
                    _profileFileService?.Dispose();
                }
            };
            this.Activated += async (s, e) =>
            {
                if (_vm.Groups.Count == 0)
                {
                    await _vm.LoadAsync(this.DispatcherQueue);
                }

                // Initialize profile list on first activation
                InitializeProfilesList();
            };

            // Keep left pane visible when no selection so UI doesn't look empty
            _vm.PropertyChanged += ViewModel_PropertyChanged;

            // Try to initialize profile list early
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(200); // Give UI time to load
                InitializeProfilesList();
            });

            // Fallback: also trigger once the visual tree has definitely loaded to avoid first-open empty list race.
            AttachRootLoadedHandler();

            // Modern styling applied via InitializeWindowStyling
        }

        // Legacy compatibility constructor (kept for existing call sites): delegates to runtime-aware overload.
        public SettingsWindow(Services.Profiles.IProfileManager profileManager = null, Services.Profiles.ProfileFileService profileFileService = null)
            : this(null, profileManager)
        {
            if (profileFileService != null)
            {
                // Override file service if explicitly supplied (rare). Mark ownership false.
                _profileFileService = profileFileService;
                _ownsFileService = false;
            }
        }

        private void InitializeWindowStyling()
        {
            // Try set Mica backdrop (Base for subtle tint)
            try
            {
                var mica = new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base,
                };
                SystemBackdrop = mica;
            }
            catch
            {
            }

            // Extend into title bar & customize caption buttons
            try
            {
                if (AppWindow?.TitleBar != null)
                {
                    var tb = AppWindow.TitleBar;
                    tb.ExtendsContentIntoTitleBar = true;
                    tb.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Standard;
                    tb.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    tb.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(24, 0, 0, 0);
                    tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(36, 0, 0, 0);
                }

                _appTitleBarCache ??= GetAppTitleBar();
                if (_appTitleBarCache is FrameworkElement dragRegion)
                {
                    this.SetTitleBar(dragRegion);
                }
            }
            catch
            {
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedGroup) ||
                e.PropertyName == nameof(SettingsViewModel.HasNoSelectedGroup))
            {
                EnsureLeftPaneColumn();
                _leftPaneColumnCache ??= GetLeftPaneColumn();
                if (_leftPaneColumnCache != null && _vm.HasNoSelectedGroup)
                {
                    _leftPaneColumnCache.Width = new GridLength(240);
                }
            }
        }

        private void OnToggleGroupsPane(object sender, RoutedEventArgs e)
        {
            EnsureLeftPaneColumn();
            if (_leftPaneColumnCache != null)
            {
                _leftPaneColumnCache.Width = (_leftPaneColumnCache.Width.Value == 0) ? new GridLength(240) : new GridLength(0);
            }
        }

        private async void OnAddGroup(object sender, RoutedEventArgs e)
        {
            _vm.AddGroup();
            await _vm.SaveAsync();
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            await _vm.SaveAsync();
        }

        private async void OnClose(object sender, RoutedEventArgs e)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true; // prevent re-entry

            try
            {
                await _vm.SaveAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    SafeLogWarning($"Save before close failed: {ex.Message}");
                }
                catch
                {
                }
            }

            SafeCloseWindow();
        }

        private async void OnAddButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.AddButton(_vm.SelectedGroup);
                await _vm.SaveAsync();
            }
        }

        private async void OnRemoveGroup(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag;
            var group = (tag as ButtonGroup) ?? (_vm.Groups.Contains(_vm.SelectedGroup) ? _vm.SelectedGroup : null);
            if (group != null)
            {
                _vm.RemoveGroup(group);
                await _vm.SaveAsync();
            }
        }

        private async void OnRemoveSelectedGroup(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup != null)
            {
                _vm.RemoveGroup(_vm.SelectedGroup);
                await _vm.SaveAsync();
            }
        }

        // Profile management event handlers
        private async void OnAddProfile(object sender, RoutedEventArgs e)
        {
            try
            {
                var newProfileId = "profile-" + DateTime.Now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var newProfileName = $"Profile {DateTime.Now:HH:mm}";

                // Create empty profile
                var newProfile = _profileFileService.CreateEmptyProfile(newProfileId, newProfileName);

                // Save the profile
                _profileFileService.SaveProfile(newProfile);

                // Refresh the UI
                await RefreshProfilesList();

                SafeLogWarning($"Created new profile: {newProfileName}");
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to add profile: {ex.Message}");
            }
        }

        private void OnRemoveSelectedProfile(object sender, RoutedEventArgs e)
        {
            if (_profileManager == null)
            {
                return;
            }

            try
            {
                // For now, we'll implement this differently since we can't easily access ProfilesList
                // Will be implemented once we have a proper reference
                SafeLogWarning("Profile removal not yet implemented");
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to remove profile: {ex.Message}");
            }
        }

        private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var listView = sender as ListView;
                var selectedProfileMeta = listView?.SelectedItem as Services.Profiles.Models.ProfileMeta;
                if (selectedProfileMeta != null)
                {
                    SafeLogWarning($"Profile selection changed to: {selectedProfileMeta.Id} - {selectedProfileMeta.Name}");

                    // Load full profile from ProfileFileService
                    var fullProfile = _profileFileService.GetProfile(selectedProfileMeta.Id);
                    SafeLogWarning($"Loaded full profile: {fullProfile?.Name ?? "null"} with {fullProfile?.Groups?.Count ?? 0} groups");

                    SelectedProfile = fullProfile;

                    // Also update legacy profile manager if available
                    _profileManager?.SwitchProfile(selectedProfileMeta.Id);
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to switch profile: {ex.Message}");
            }
        }

        private void OnStartRenameProfile(object sender, RoutedEventArgs e)
        {
            // Profile rename logic similar to group rename
            var button = sender as Button;
            var profile = button?.DataContext as Services.Profiles.Models.ProfileMeta;
            if (profile != null)
            {
                // Find the corresponding UI elements and switch to edit mode
                // Implementation similar to OnStartRenameGroup
            }
        }

        private async void OnProfileNameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Escape)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    await CommitProfileNameEdit(textBox, e.Key == Windows.System.VirtualKey.Enter);
                }
            }
        }

        private async void OnProfileNameTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                await CommitProfileNameEdit(textBox, true);
            }
        }

        private async System.Threading.Tasks.Task CommitProfileNameEdit(TextBox textBox, bool save)
        {
            if (_profileManager == null)
            {
                return;
            }

            try
            {
                if (save && textBox.DataContext is Services.Profiles.Models.ProfileMeta profile)
                {
                    var newName = textBox.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(newName) && newName != profile.Name)
                    {
                        _profileManager.RenameProfile(profile.Id, newName);
                        await RefreshProfilesList();
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to rename profile: {ex.Message}");
            }
            finally
            {
                // Switch back to display mode (similar to group rename logic)
            }
        }

        private void UpdateProfileUI()
        {
            try
            {
                // Find and update profile-related UI elements
                if (this.Content is FrameworkElement root)
                {
                    var profileSettingsCard = FindChildByName(root, "ProfileSettingsCard") as FrameworkElement;
                    var selectedProfileNameText = FindChildByName(root, "SelectedProfileNameText") as TextBlock;
                    var actionsHeaderPanel = FindChildByName(root, "ActionsHeaderPanel") as FrameworkElement;
                    var actionsScrollViewer = FindChildByName(root, "ActionsScrollViewer") as FrameworkElement;

                    if (_selectedProfile != null)
                    {
                        // Show profile-related UI
                        if (profileSettingsCard != null)
                        {
                            profileSettingsCard.Visibility = Visibility.Visible;
                        }

                        if (selectedProfileNameText != null)
                        {
                            selectedProfileNameText.Text = _selectedProfile.Name ?? "Unknown Profile";
                        }

                        if (actionsHeaderPanel != null)
                        {
                            actionsHeaderPanel.Visibility = Visibility.Visible;
                        }

                        if (actionsScrollViewer != null)
                        {
                            actionsScrollViewer.Visibility = Visibility.Visible;
                        }

                        SafeLogWarning($"Profile UI updated for: {_selectedProfile.Name}");
                    }
                    else
                    {
                        // Hide profile-related UI
                        if (profileSettingsCard != null)
                        {
                            profileSettingsCard.Visibility = Visibility.Collapsed;
                        }

                        if (actionsHeaderPanel != null)
                        {
                            actionsHeaderPanel.Visibility = Visibility.Collapsed;
                        }

                        if (actionsScrollViewer != null)
                        {
                            actionsScrollViewer.Visibility = Visibility.Collapsed;
                        }

                        SafeLogWarning("Profile UI updated for: None");
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to update profile UI: {ex.Message}");
            }
        }

        private void UpdateActionsUI()
        {
            try
            {
                // Ensure we're on the UI thread
                if (!this.DispatcherQueue.HasThreadAccess)
                {
                    if (_disposed || _isClosed)
                    {
                        return;
                    }

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_disposed || _isClosed)
                        {
                            return;
                        }

                        try
                        {
                            UpdateActionsUI();
                        }
                        catch (Exception ex)
                        {
                            SafeLogWarning($"Deferred UpdateActionsUI failed: {ex.Message}");
                        }
                    });
                    return;
                }

                // Find the Actions UI elements
                if (!_disposed && !_isClosed && this.Content is FrameworkElement root)
                {
                    // First make sure the ActionsScrollViewer is visible
                    var actionsScrollViewer = FindChildByName(root, "ActionsScrollViewer") as ScrollViewer;
                    if (actionsScrollViewer != null)
                    {
                        actionsScrollViewer.Visibility = Visibility.Visible;
                        SafeLogWarning("Made ActionsScrollViewer visible");
                    }
                    else
                    {
                        SafeLogWarning("ActionsScrollViewer control not found");
                    }

                    var actionsPanel = FindChildByName(root, "ActionsPanel") as StackPanel;
                    if (actionsPanel != null)
                    {
                        SafeLogWarning($"Found ActionsPanel, clearing {actionsPanel.Children.Count} existing children");

                        // Clear existing content
                        actionsPanel.Children.Clear();

                        // Add groups and their actions
                        foreach (var group in _currentProfileGroups)
                        {
                            // Create group header
                            var groupExpander = new CommunityToolkit.WinUI.Controls.SettingsExpander
                            {
                                Header = group.Name,
                                Description = group.Description,
                                IsExpanded = true,
                                Margin = new Thickness(0, 0, 0, 16),
                            };

                            // Add group toggle
                            var groupToggle = new ToggleSwitch
                            {
                                IsOn = group.IsEnabled,
                                Tag = group,
                            };
                            groupToggle.Toggled += OnGroupToggled;
                            groupExpander.HeaderIcon = new FontIcon { Glyph = "\uE8A5" }; // Group icon

                            // Add actions to group
                            foreach (var action in group.Actions)
                            {
                                var actionCard = new CommunityToolkit.WinUI.Controls.SettingsCard
                                {
                                    Header = action.DisplayName,
                                    Description = action.Description,
                                };

                                var actionToggle = new ToggleSwitch
                                {
                                    IsOn = action.IsEnabled,
                                    Tag = action,
                                };
                                actionToggle.Toggled += OnActionToggled;
                                actionCard.Content = actionToggle;

                                groupExpander.Items.Add(actionCard);
                            }

                            // Set group toggle as main content
                            groupExpander.Content = groupToggle;
                            actionsPanel.Children.Add(groupExpander);
                        }

                        SafeLogWarning($"Actions UI updated with {_currentProfileGroups.Count} groups");
                    }
                    else
                    {
                        SafeLogWarning("ActionsPanel control not found - checking if parent container is collapsed");
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to update actions UI: {ex.Message}");
            }
        }

        private void OnGroupToggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var toggleSwitch = sender as ToggleSwitch;
                var group = toggleSwitch?.Tag as Models.ProfileGroup;

                if (group != null && _selectedProfile != null)
                {
                    group.IsEnabled = toggleSwitch.IsOn;

                    // Save the updated profile
                    _profileFileService.SaveProfile(_selectedProfile);

                    // Notify ToolbarWindow that the profile has been updated
                    System.Diagnostics.Debug.WriteLine($"SettingsWindow.OnGroupToggled: Group {group.Name} toggled to {toggleSwitch.IsOn}, notifying profile runtime");
                    _profileRuntime?.NotifyActiveProfileUpdated();

                    SafeLogWarning($"Group {group.Name} is now {(group.IsEnabled ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to toggle group: {ex.Message}");
            }
        }

        private void OnActionToggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var toggleSwitch = sender as ToggleSwitch;
                var action = toggleSwitch?.Tag as Models.ProfileAction;

                if (action != null && _selectedProfile != null)
                {
                    action.IsEnabled = toggleSwitch.IsOn;

                    // Save the updated profile
                    _profileFileService.SaveProfile(_selectedProfile);

                    // Notify ToolbarWindow that the profile has been updated
                    System.Diagnostics.Debug.WriteLine($"SettingsWindow.OnActionToggled: Action {action.DisplayName} toggled to {toggleSwitch.IsOn}, notifying profile runtime");
                    _profileRuntime?.NotifyActiveProfileUpdated();

                    SafeLogWarning($"Action {action.DisplayName} is now {(action.IsEnabled ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to toggle action: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task RefreshProfilesList()
        {
            try
            {
                // Get profiles from ProfileFileService
                var profiles = _profileFileService.GetAllProfiles();

                // Convert to ProfileMeta for ListView binding (maintaining compatibility)
                var profileMetas = profiles.Select(p => new Services.Profiles.Models.ProfileMeta
                {
                    Id = p.Id,
                    Name = p.Name,
                }).ToList();

                // Try to find and update the ListView
                var success = await TryUpdateProfilesList(profileMetas);

                if (!success)
                {
                    // Retry after a short delay if UI is not ready
                    await System.Threading.Tasks.Task.Delay(100);
                    await TryUpdateProfilesList(profileMetas);
                }

                SafeLogWarning($"Profile list refreshed with {profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to refresh profiles list: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task<bool> TryUpdateProfilesList(System.Collections.Generic.List<Services.Profiles.Models.ProfileMeta> profileMetas)
        {
            if (_disposed || _isClosed)
            {
                return false;
            }

            try
            {
                var result = false;

                // Fast exit if window already torn down
                if (this.AppWindow == null)
                {
                    return false;
                }

                // Guard dispatch with IsWindowClosed style check
                if (!this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (_disposed || _isClosed)
                        {
                            return;
                        }

                        var content = this.Content; // may throw if closed
                        if (content is FrameworkElement root)
                        {
                            var profilesList = FindChildByName(root, "ProfilesList") as ListView;
                            if (profilesList != null)
                            {
                                profilesList.ItemsSource = profileMetas;
                                result = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeLogWarning($"TryUpdateProfilesList enqueue failed: {ex.Message}");
                    }
                }))
                {
                    return false;
                }

                await System.Threading.Tasks.Task.Delay(40);
                return result;
            }
            catch (Exception ex)
            {
                SafeLogWarning($"TryUpdateProfilesList outer failed: {ex.Message}");
                return false;
            }
        }

        // Helper method to find controls by name in the visual tree
        private FrameworkElement FindChildByName(DependencyObject parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element && element.Name == name)
                {
                    return element;
                }

                var result = FindChildByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private async void InitializeProfilesList()
        {
            try
            {
                await RefreshProfilesList();
            }
            catch (Exception ex)
            {
                SafeLogWarning($"Failed to initialize profiles list: {ex.Message}");
            }
        }

        // Wire a one-time Loaded handler on the root content element (WindowEx itself has no Loaded event)
        private void AttachRootLoadedHandler()
        {
            try
            {
                if (this.Content is FrameworkElement fe)
                {
                    RoutedEventHandler handler = null;
                    handler = (s, e) =>
                    {
                        fe.Loaded -= handler;
                        if (_disposed || _isClosed)
                        {
                            return;
                        }

                        SafeLogWarning("Root Loaded event fired; ensuring ProfilesList binds.");
                        InitializeProfilesList();
                    };
                    fe.Loaded += handler;
                }
                else
                {
                    // If content not yet assigned, schedule a retry shortly.
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        if (_disposed || _isClosed)
                        {
                            return;
                        }

                        AttachRootLoadedHandler();
                    });
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning("AttachRootLoadedHandler failed: " + ex.Message);
            }
        }

        private static async Task ShowSimpleMessageAsync(XamlRoot xamlRoot, string title, string message)
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

        private async void OnSnapshotWorkspace(object sender, RoutedEventArgs e)
        {
            if (_disposed || _isClosed)
            {
                return;
            }

            if (Content is not FrameworkElement root)
            {
                return;
            }

            var nameBox = new TextBox
            {
                PlaceholderText = "Workspace name",
            };

            if (root.Resources != null && root.Resources.TryGetValue("StandardTextBoxStyle", out var styleObj) && styleObj is Style textBoxStyle)
            {
                nameBox.Style = textBoxStyle;
            }

            var dialogContent = new StackPanel
            {
                Spacing = 12,
            };
            dialogContent.Children.Add(new TextBlock
            {
                Text = "Enter a name for the new workspace snapshot.",
                TextWrapping = TextWrapping.Wrap,
            });
            dialogContent.Children.Add(nameBox);

            var dialog = new ContentDialog
            {
                XamlRoot = root.XamlRoot,
                Title = "Create workspace snapshot",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = dialogContent,
                IsPrimaryButtonEnabled = false,
            };

            nameBox.TextChanged += (_, __) =>
            {
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var workspaceName = nameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(workspaceName))
            {
                return;
            }

            try
            {
                using var workspaceProvider = new WorkspaceProvider();
                var workspace = await workspaceProvider.SnapshotAsync(workspaceName, CancellationToken.None).ConfigureAwait(true);
                if (workspace == null)
                {
                    await ShowSimpleMessageAsync(root.XamlRoot, "Snapshot failed", "No eligible windows were detected to capture.");
                    return;
                }

                await ShowSimpleMessageAsync(root.XamlRoot, "Snapshot saved", $"Workspace \"{workspace.Name}\" has been saved.");
            }
            catch (Exception ex)
            {
                await ShowSimpleMessageAsync(root.XamlRoot, "Snapshot failed", ex.Message);
            }
        }

        private async void OnRemoveButton(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedGroup == null)
            {
                return;
            }

            var targetButton = (sender as FrameworkElement)?.DataContext as ToolbarButton ?? _vm.SelectedButton;
            if (targetButton == null)
            {
                return;
            }

            _vm.RemoveButton(_vm.SelectedGroup, targetButton);
            await _vm.SaveAsync();
        }

        private async void OnBrowseIcon(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedButton == null)
            {
                return;
            }

            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".ico");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _vm.SelectedButton.IconType = ToolbarIconType.Image;
                _vm.SelectedButton.IconPath = file.Path;
                await _vm.SaveAsync();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _vm?.Dispose();
            }
            catch
            {
            }
        }

        // InitializeWindowStyling removed.
        private void ToggleMaximize()
        {
            try
            {
                if (AppWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                {
                    if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                    {
                        p.Restore();
                    }
                    else
                    {
                        p.Maximize();
                    }
                }
            }
            catch
            {
            }
        }

        private ColumnDefinition _leftPaneColumnCache;

        private void EnsureLeftPaneColumn()
        {
            if (_leftPaneColumnCache == null)
            {
                _leftPaneColumnCache = GetLeftPaneColumn();
            }
        }

        private ColumnDefinition GetLeftPaneColumn()
        {
            try
            {
                // The left pane ColumnDefinition has x:Name="LeftPaneColumn" in XAML. Generated partial may expose field; if not, locate via visual tree.
                var root = this.Content as FrameworkElement;
                if (root != null)
                {
                    return (ColumnDefinition)root.FindName("LeftPaneColumn");
                }
            }
            catch
            {
            }

            return null;
        }

        private FrameworkElement GetAppTitleBar()
        {
            try
            {
                var root = this.Content as FrameworkElement;
                if (root != null)
                {
                    return root.FindName("AppTitleBar") as FrameworkElement;
                }
            }
            catch
            {
            }

            return null;
        }

        // Removed manual BeginDragMove implementation: using SetTitleBar now.
        private void SafeCloseWindow()
        {
            try
            {
                Close();
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                // Ignore RO_E_CLOSED or already closed window scenarios
                try
                {
                    SafeLogWarning($"Close COMException 0x{comEx.HResult:X}");
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                try
                {
                    SafeLogError($"Unexpected Close exception: {ex.Message}");
                }
                catch
                {
                }
            }
        }

        private static void SafeLogWarning(string msg)
        {
#if HAS_MANAGEDCOMMON_LOGGER
            try { ManagedCommon.Logger.LogWarning("SettingsWindow: " + msg); } catch { }
#else
            Debug.WriteLine("[SettingsWindow][WARN] " + msg);
#endif
        }

        private static void SafeLogError(string msg)
        {
#if HAS_MANAGEDCOMMON_LOGGER
            try { ManagedCommon.Logger.LogError("SettingsWindow: " + msg); } catch { }
#else
            Debug.WriteLine("[SettingsWindow][ERR ] " + msg);
#endif
        }

        // Removed P/Invoke (ReleaseCapture / SendMessage) no longer required.

        // Inline rename handlers for groups list
        private void OnStartRenameGroup(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is ButtonGroup group)
                {
                    // Ensure this group is selected
                    if (_vm.SelectedGroup != group)
                    {
                        _vm.SelectedGroup = group;
                    }

                    // Find ListViewItem visual tree, then TextBox
                    // Access GroupsList via root FrameworkElement (Window itself has no FindName in WinUI 3)
                    var root = this.Content as FrameworkElement;
                    var groupsList = root?.FindName("GroupsList") as ListView;
                    var container = groupsList?.ContainerFromItem(group) as ListViewItem;
                    if (container != null)
                    {
                        var editBox = FindChild<TextBox>(container, "NameEdit");
                        var textBlock = FindChild<TextBlock>(container, "NameText");
                        if (editBox != null && textBlock != null)
                        {
                            textBlock.Visibility = Visibility.Collapsed;
                            editBox.Visibility = Visibility.Visible;
                            editBox.SelectAll();
                            _ = editBox.Focus(FocusState.Programmatic);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogWarning("OnStartRenameGroup: " + ex.Message);
            }
        }

        private void CommitGroupRename(TextBox editBox, TextBlock textBlock)
        {
            if (editBox == null || textBlock == null)
            {
                return;
            }

            textBlock.Visibility = Visibility.Visible;
            editBox.Visibility = Visibility.Collapsed;
        }

        private void OnGroupNameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    var parent = tb.Parent as FrameworkElement;
                    var textBlock = FindSibling<TextBlock>(tb, "NameText");
                    CommitGroupRename(tb, textBlock);
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    // Revert displayed text (binding already updated progressively, so we reload from VM selected group name)
                    if (_vm.SelectedGroup != null)
                    {
                        tb.Text = _vm.SelectedGroup.Name;
                    }

                    var textBlock = FindSibling<TextBlock>(tb, "NameText");
                    CommitGroupRename(tb, textBlock);
                    e.Handled = true;
                }
            }
        }

        private void OnGroupNameTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                var textBlock = FindSibling<TextBlock>(tb, "NameText");
                CommitGroupRename(tb, textBlock);
            }
        }

        // Utility visual tree search helpers
        private static T FindChild<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T fe)
                {
                    if (string.IsNullOrEmpty(name) || fe.Name == name)
                    {
                        return fe;
                    }
                }

                var result = FindChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static T FindSibling<T>(FrameworkElement element, string name)
            where T : FrameworkElement
        {
            if (element?.Parent is DependencyObject parent)
            {
                return FindChild<T>(parent, name);
            }

            return null;
        }

        // Inline group description editing removed per design update; now always displays single-line text.
    }
}

