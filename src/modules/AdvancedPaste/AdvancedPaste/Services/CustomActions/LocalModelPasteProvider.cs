// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class LocalModelPasteProvider : IPasteAIProvider
    {
        private readonly string _localModelPath;

        public LocalModelPasteProvider(string localModelPath)
        {
            _localModelPath = localModelPath;
        }

        public string ProviderName => "local";

        public string DisplayName => "Local Model";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<PasteAIProviderResult> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
        {
            // TODO: Implement local model inference logic
            var content = request?.ChatHistory?.LastOrDefault()?.Content ?? string.Empty;
            return Task.FromResult(new PasteAIProviderResult(content, AIServiceUsage.None));
        }
    }
}
