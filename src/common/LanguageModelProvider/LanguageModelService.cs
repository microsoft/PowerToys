// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace LanguageModelProvider;

public sealed class LanguageModelService
{
    private readonly ConcurrentDictionary<string, ILanguageModelProvider> _providersByPrefix;

    public LanguageModelService(IEnumerable<ILanguageModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providersByPrefix = new ConcurrentDictionary<string, ILanguageModelProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.UrlPrefix))
            {
                _providersByPrefix[provider.UrlPrefix] = provider;
            }
        }
    }

    public static LanguageModelService CreateDefault()
    {
        return new LanguageModelService(new[]
        {
            FoundryLocalModelProvider.Instance,
        });
    }

    public IReadOnlyCollection<ILanguageModelProvider> Providers => _providersByPrefix.Values.ToArray();

    public bool RegisterProvider(ILanguageModelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.UrlPrefix))
        {
            throw new ArgumentException("Provider must supply a URL prefix.", nameof(provider));
        }

        _providersByPrefix[provider.UrlPrefix] = provider;
        return true;
    }

    public ILanguageModelProvider? GetProviderFor(string? modelReference)
    {
        if (string.IsNullOrWhiteSpace(modelReference))
        {
            return null;
        }

        foreach (var provider in _providersByPrefix.Values)
        {
            if (modelReference.StartsWith(provider.UrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return provider;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<ModelDetails>> GetModelsAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        List<ModelDetails> models = [];

        foreach (var provider in _providersByPrefix.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var providerModels = await provider.GetModelsAsync(refresh, cancellationToken).ConfigureAwait(false);
            models.AddRange(providerModels);
        }

        return models;
    }

    public IChatClient? GetClient(ModelDetails model)
    {
        if (model is null)
        {
            return null;
        }

        var reference = !string.IsNullOrWhiteSpace(model.Url) ? model.Url : model.Id;
        return GetClient(reference);
    }

    public IChatClient? GetClient(string? modelReference)
    {
        if (string.IsNullOrWhiteSpace(modelReference))
        {
            return null;
        }

        var provider = GetProviderFor(modelReference);

        return provider?.GetIChatClient(modelReference);
    }
}
