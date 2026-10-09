// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.AI;

namespace LanguageModelProvider;

public interface ILanguageModelProvider
{
    string Name { get; }

    string ProviderDescription { get; }

    Task<IEnumerable<ModelDetails>> GetModelsAsync(CancellationToken cancelationToken = default);

    IChatClient? GetIChatClient(string modelId);

    string GetIChatClientString(string url);
}
