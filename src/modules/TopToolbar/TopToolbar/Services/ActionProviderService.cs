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
using TopToolbar.Providers;

namespace TopToolbar.Services
{
    public sealed class ActionProviderService
    {
        private readonly ActionProviderRuntime _runtime;

        public ActionProviderService(ActionProviderRuntime runtime)
        {
            _runtime = runtime ?? new ActionProviderRuntime();
        }

        public IReadOnlyCollection<string> RegisteredProviderIds => _runtime.RegisteredProviderIds;

        public IReadOnlyCollection<string> RegisteredGroupProviderIds => _runtime.RegisteredGroupProviderIds;

        public void RegisterProvider(IActionProvider provider)
        {
            _runtime.RegisterProvider(provider);
        }

        public Task<ProviderInfo> GetInfoAsync(string providerId, CancellationToken cancellationToken)
        {
            return _runtime.GetInfoAsync(providerId, cancellationToken);
        }

        public Task<ButtonGroup> CreateGroupAsync(string providerId, ActionContext context, CancellationToken cancellationToken)
        {
            return _runtime.CreateGroupAsync(providerId, context, cancellationToken);
        }

        public Task<IReadOnlyList<ActionDescriptor>> DiscoverAsync(
            IEnumerable<string> providerIds,
            ActionContext context,
            CancellationToken cancellationToken)
        {
            return _runtime.DiscoverAsync(providerIds, context, cancellationToken);
        }

        public Task<ActionResult> InvokeAsync(
            string providerId,
            string actionId,
            JsonElement? args,
            ActionContext context,
            IProgress<ActionProgress> progress,
            CancellationToken cancellationToken)
        {
            return _runtime.InvokeAsync(providerId, actionId, args, context, progress, cancellationToken);
        }

        public bool TryGetProvider(string providerId, out IActionProvider provider)
        {
            return _runtime.TryGetProvider(providerId, out provider);
        }
    }
}
