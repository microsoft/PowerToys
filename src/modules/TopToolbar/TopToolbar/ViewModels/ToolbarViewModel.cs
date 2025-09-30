// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Services;

namespace TopToolbar.ViewModels
{
    public class ToolbarViewModel : ObservableObject, IDisposable
    {
        private readonly ToolbarConfigService _configService;
        private readonly ActionProviderService _providerService;
        private readonly ActionContextFactory _contextFactory;
        private readonly HashSet<string> _staticGroupIds = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _loadCts = new();

        public ObservableCollection<ButtonGroup> Groups { get; } = new();

        public ToolbarViewModel(
            ToolbarConfigService configService,
            ActionProviderService providerService,
            ActionContextFactory contextFactory)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task LoadAsync(DispatcherQueue dispatcher)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            ToolbarConfig config;
            try
            {
                config = await _configService.LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ToolbarViewModel.LoadAsync: failed to load toolbar config", ex);
                return;
            }

            var combinedGroups = new List<ButtonGroup>();
            _staticGroupIds.Clear();

            try
            {
                foreach (var group in config.Groups ?? Enumerable.Empty<ButtonGroup>())
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var clone = CloneGroup(group);
                        if (string.IsNullOrWhiteSpace(clone.Id))
                        {
                            clone.Id = Guid.NewGuid().ToString();
                        }

                        NormalizeGroup(clone);
                        combinedGroups.Add(clone);
                        _staticGroupIds.Add(clone.Id);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError($"ToolbarViewModel.LoadAsync: failed to clone static group '{group?.Id ?? "<null>"}'", ex);
                    }
                }

                var providerGroups = await BuildProviderGroupsAsync(token).ConfigureAwait(false);
                combinedGroups.AddRange(providerGroups);

                await ApplyGroupsAsync(dispatcher, combinedGroups, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                AppLogger.LogInfo("ToolbarViewModel.LoadAsync: load cancelled.");
            }
        }

        public async Task SaveAsync()
        {
            var config = new ToolbarConfig();

            foreach (var group in Groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.Id) || !_staticGroupIds.Contains(group.Id))
                {
                    continue;
                }

                var clone = CloneGroup(group);
                for (int i = clone.Buttons.Count - 1; i >= 0; i--)
                {
                    var action = clone.Buttons[i]?.Action;
                    if (action != null && action.Type == ToolbarActionType.Provider)
                    {
                        clone.Buttons.RemoveAt(i);
                    }
                }

                config.Groups.Add(clone);
            }

            await _configService.SaveAsync(config).ConfigureAwait(false);
        }

        private async Task<IList<ButtonGroup>> BuildProviderGroupsAsync(CancellationToken cancellationToken)
        {
            var results = new List<ButtonGroup>();
            var context = _contextFactory.CreateForDiscovery(null);

            foreach (var providerId in _providerService.RegisteredGroupProviderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var group = await _providerService.CreateGroupAsync(providerId, context, cancellationToken).ConfigureAwait(false);
                    if (group == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(group.Id))
                    {
                        group.Id = $"provider::{providerId}";
                    }

                    var clone = CloneGroup(group);
                    NormalizeGroup(clone);
                    results.Add(clone);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"ToolbarViewModel.BuildProviderGroupsAsync: provider '{providerId}' failed to produce a group.", ex);
                }
            }

            return results;
        }

        private static void NormalizeGroup(ButtonGroup group)
        {
            group.Layout ??= new ToolbarGroupLayout();
            group.Buttons ??= new ObservableCollection<ToolbarButton>();

            double order = 0;
            foreach (var button in group.Buttons.Where(b => b != null))
            {
                if (string.IsNullOrWhiteSpace(button.Id))
                {
                    button.Id = Guid.NewGuid().ToString();
                }

                button.Action ??= new ToolbarAction();

                if (!button.SortOrder.HasValue)
                {
                    button.SortOrder = order;
                }

                order += 1;
            }
        }

        private static ButtonGroup CloneGroup(ButtonGroup source)
        {
            if (source == null)
            {
                return new ButtonGroup();
            }

            var clone = new ButtonGroup
            {
                Id = source.Id,
                Name = source.Name,
                Description = source.Description,
                IsEnabled = source.IsEnabled,
                Filter = source.Filter,
                AutoRefresh = source.AutoRefresh,
                Layout = CloneLayout(source.Layout),
            };

            if (source.Providers != null)
            {
                foreach (var provider in source.Providers)
                {
                    clone.Providers.Add(provider);
                }
            }

            if (source.StaticActions != null)
            {
                foreach (var action in source.StaticActions)
                {
                    clone.StaticActions.Add(action);
                }
            }

            if (source.Buttons != null)
            {
                foreach (var button in source.Buttons)
                {
                    if (button != null)
                    {
                        clone.Buttons.Add(button.Clone());
                    }
                }
            }

            return clone;
        }

        private static ToolbarGroupLayout CloneLayout(ToolbarGroupLayout layout)
        {
            if (layout == null)
            {
                return new ToolbarGroupLayout();
            }

            return new ToolbarGroupLayout
            {
                Style = layout.Style,
                Overflow = layout.Overflow,
                MaxInline = layout.MaxInline,
                ShowLabels = layout.ShowLabels,
            };
        }

        private async Task ApplyGroupsAsync(DispatcherQueue dispatcher, IList<ButtonGroup> groups, CancellationToken token)
        {
            void Apply()
            {
                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(group);
                }
            }

            if (dispatcher == null || dispatcher.HasThreadAccess)
            {
                Apply();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            if (!dispatcher.TryEnqueue(() =>
            {
                if (token.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(token);
                    return;
                }

                Apply();
                tcs.TrySetResult(true);
            }))
            {
                AppLogger.LogWarning("ToolbarViewModel.ApplyGroupsAsync: failed to marshal to dispatcher, applying on caller thread.");
                Apply();
                return;
            }

            await tcs.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
