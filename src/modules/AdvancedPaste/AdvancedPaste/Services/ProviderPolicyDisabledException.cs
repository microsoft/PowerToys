// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;

namespace AdvancedPaste.Services;

internal sealed class ProviderPolicyDisabledException : PasteActionException
{
    public ProviderPolicyDisabledException()
        : base("The selected AI provider is disabled by policy.", null)
    {
    }
}
