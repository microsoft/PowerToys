// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Registration of an AI chat provider factory for one or more <see cref="AIServiceType"/> values.
/// </summary>
public sealed class AIChatProviderRegistration
{
    public AIChatProviderRegistration(
        IReadOnlyCollection<AIServiceType> supportedTypes,
        Func<AIProviderConfig, IAIChatProvider> factory)
    {
        SupportedTypes = supportedTypes ?? throw new ArgumentNullException(nameof(supportedTypes));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IReadOnlyCollection<AIServiceType> SupportedTypes { get; }

    public Func<AIProviderConfig, IAIChatProvider> Factory { get; }
}
