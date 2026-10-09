// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class LocalModelPasteProvider : IPasteAIProvider
    {
        private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
        {
            AIServiceType.Onnx,
            AIServiceType.ML,
        };

        public static PasteAIProviderRegistration Registration { get; } = new(SupportedTypes, config => new LocalModelPasteProvider(config));

        private readonly PasteAIConfig _config;

        public LocalModelPasteProvider(PasteAIConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<string> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(request);

            // TODO: Implement local model inference logic using _config.LocalModelPath/_config.ModelPath
            var content = request.InputText ?? string.Empty;
            request.Usage = AIServiceUsage.None;
            return Task.FromResult(content);
        }
    }
}
