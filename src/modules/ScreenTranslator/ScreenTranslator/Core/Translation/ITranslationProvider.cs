// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Abstraction for translation backends.
/// </summary>
public interface ITranslationProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default);
}
