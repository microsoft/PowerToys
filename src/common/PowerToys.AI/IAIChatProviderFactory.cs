// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Creates <see cref="IAIChatProvider"/> instances from runtime configuration.
/// </summary>
public interface IAIChatProviderFactory
{
    IAIChatProvider CreateProvider(AIProviderConfig config);
}
