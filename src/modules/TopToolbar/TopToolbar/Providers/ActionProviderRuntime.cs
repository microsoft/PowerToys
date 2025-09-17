// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;

namespace TopToolbar.Providers
{
    public sealed class ActionProviderRuntime
    {
        private readonly Dictionary<string, IActionProvider> _providers;
        private readonly Dictionary<string, ProviderInfo> _infoCache;

        public ActionProviderRuntime()
        {
            _providers = new Dictionary<string, IActionProvider>(StringComparer.OrdinalIgnoreCase);
            _infoCache = new Dictionary<string, ProviderInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> RegisteredProviderIds => _providers.Keys;

        public void RegisterProvider(IActionProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new InvalidOperationException("Provider must expose a non-empty Id.");
            }

            _providers[provider.Id] = provider;
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
