// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class PasteAIProviderRegistration
    {
        public PasteAIProviderRegistration(IReadOnlyCollection<Microsoft.PowerToys.Settings.UI.Library.AIServiceType> supportedTypes, Func<PasteAIConfig, IPasteAIProvider> factory)
        {
            SupportedTypes = supportedTypes ?? throw new ArgumentNullException(nameof(supportedTypes));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IReadOnlyCollection<Microsoft.PowerToys.Settings.UI.Library.AIServiceType> SupportedTypes { get; }

        public Func<PasteAIConfig, IPasteAIProvider> Factory { get; }
    }
}
