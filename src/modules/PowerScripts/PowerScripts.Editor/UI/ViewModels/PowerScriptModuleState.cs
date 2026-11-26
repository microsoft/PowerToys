// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.UI.ViewModels;

internal sealed class PowerScriptModuleState(bool enabled)
{
    private bool? _pendingValue;

    public bool IsEnabled { get; private set; } = enabled;

    public void Request(bool value)
    {
        _pendingValue = value;
        IsEnabled = value;
    }

    public bool ApplySnapshot(bool value, bool policyControlled)
    {
        if (!policyControlled && _pendingValue is { } pending && pending != value)
        {
            return false;
        }

        _pendingValue = null;
        IsEnabled = value;
        return true;
    }
}
