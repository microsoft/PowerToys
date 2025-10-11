// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LanguageModelProvider.FoundryLocal;

internal sealed record FoundryModelDownload(
    string Name,
    string Uri,
    string Path,
    string ProviderType,
    PromptTemplate PromptTemplate);
