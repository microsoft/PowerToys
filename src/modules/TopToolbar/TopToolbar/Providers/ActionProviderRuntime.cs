// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Models;

namespace TopToolbar.Providers
{
    public sealed class ActionProviderRuntime
    {
        private readonly Dictionary<string, IActionProvider> _providers;
        private readonly Dictionary<string, ProviderInfo> _infoCache;
        private readonly Dictionary<string, IToolbarGroupProvider> _groupProviders;
        private long _changeVersion;

        /// <summary>
        /// Raised when any provider reports a change. Carries semantic detail for selective refresh.
        /// </summary>
        public event EventHandler<ProviderChangedEventArgs> ProvidersChanged;

        public ActionProviderRuntime()
        {
            _providers = new Dictionary<string, IActionProvider>(StringComparer.OrdinalIgnoreCase);
            _infoCache = new Dictionary<string, ProviderInfo>(StringComparer.OrdinalIgnoreCase);
            _groupProviders = new Dictionary<string, IToolbarGroupProvider>(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> RegisteredProviderIds => _providers.Keys;

        public IReadOnlyCollection<string> RegisteredGroupProviderIds => _groupProviders.Keys;

        public void RegisterProvider(IActionProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new InvalidOperationException("Provider must expose a non-empty Id.");
            }

            _providers[provider.Id] = provider;

            if (provider is IToolbarGroupProvider groupProvider)
            {
                _groupProviders[provider.Id] = groupProvider;
            }
            else
            {
                _groupProviders.Remove(provider.Id);
            }

            if (provider is IChangeNotifyingActionProvider notifying)
            {
                notifying.ProviderChanged += (_, args) =>
                {
                    if (args == null)
                    {
                        return;
                    }

                    try
                    {
                        // Assign version (thread-safe increment)
                        var version = System.Threading.Interlocked.Increment(ref _changeVersion);
                        args.Version = version;
                        ProvidersChanged?.Invoke(this, args);
                    }
                    catch
                    {
                    }
                };
            }
        }

        public bool TryGetProvider(string providerId, out IActionProvider provider)
        {
            return _providers.TryGetValue(providerId, out provider);
        }

        public async Task<ProviderInfo> GetInfoAsync(string providerId, CancellationToken cancellationToken)
        {
            if (!_providers.TryGetValue(providerId, out var provider))
            {
                throw new InvalidOperationException($"Provider '{providerId}' is not registered.");
            }

            if (_infoCache.TryGetValue(providerId, out var cached))
            {
                return cached;
            }

            var info = await provider.GetInfoAsync(cancellationToken).ConfigureAwait(false);
            if (info == null)
            {
                info = new ProviderInfo(providerId, string.Empty);
            }

            _infoCache[providerId] = info;
            return info;
        }

        public async Task<ButtonGroup> CreateGroupAsync(string providerId, ActionContext context, CancellationToken cancellationToken)
        {
            if (!_groupProviders.TryGetValue(providerId, out var provider))
            {
                throw new InvalidOperationException("Provider '{providerId}' does not support toolbar groups.");
            }

            var ctx = context ?? new ActionContext();
            return await provider.CreateGroupAsync(ctx, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ActionDescriptor>> DiscoverAsync(
            IEnumerable<string> providerIds,
            ActionContext context,
            CancellationToken cancellationToken)
        {
            var results = new List<ActionDescriptor>();
            var ctx = context ?? new ActionContext();

            foreach (var providerId in providerIds)
            {
                if (!_providers.TryGetValue(providerId, out var provider))
                {
                    continue;
                }

                await foreach (var descriptor in provider.DiscoverAsync(ctx, cancellationToken).ConfigureAwait(false))
                {
                    if (descriptor != null)
                    {
                        if (string.IsNullOrEmpty(descriptor.ProviderId))
                        {
                            descriptor.ProviderId = providerId;
                        }

                        results.Add(descriptor);
                    }
                }
            }

            return results;
        }

        public async Task<ActionResult> InvokeAsync(
            string providerId,
            string actionId,
            JsonElement? args,
            ActionContext context,
            IProgress<ActionProgress> progress,
            CancellationToken cancellationToken)
        {
            if (!_providers.TryGetValue(providerId, out var provider))
            {
                throw new InvalidOperationException($"Provider '{providerId}' is not registered.");
            }

            var ctx = context ?? new ActionContext();
            var progressSink = progress ?? new Progress<ActionProgress>(_ => { });

            try
            {
                var result = await provider.InvokeAsync(actionId, args, ctx, progressSink, cancellationToken).ConfigureAwait(false);
                return result ?? new ActionResult { Ok = false, Message = "Provider returned no result." };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Ok = false,
                    Message = ex.Message,
                    Output = new ActionOutput { Type = "text", Data = JsonDocument.Parse("\"" + ex.Message + "\"").RootElement },
                };
            }
        }
    }
}
