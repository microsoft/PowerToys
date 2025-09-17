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
    public interface IActionProvider
    {
        string Id { get; }

        Task<ProviderInfo> GetInfoAsync(CancellationToken cancellationToken);

        IAsyncEnumerable<ActionDescriptor> DiscoverAsync(ActionContext context, CancellationToken cancellationToken);

        Task<ActionResult> InvokeAsync(
            string actionId,
            JsonElement? args,
            ActionContext context,
            IProgress<ActionProgress> progress,
            CancellationToken cancellationToken);
    }
}
