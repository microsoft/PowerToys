// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Legacy name for <see cref="AIProviderDefaults"/>.
    /// </summary>
    [Obsolete("Use AIProviderDefaults instead.")]
    public static class PasteAIProviderDefaults
    {
        public static string GetDefaultModelName(AIServiceType serviceType) =>
            AIProviderDefaults.GetDefaultModelName(serviceType);
    }
}
