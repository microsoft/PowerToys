// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public readonly struct ListItemRealizationRegistration
{
    private readonly ListItemInitializationDemand? _demand;

    internal ListItemRealizationRegistration(ListItemInitializationDemand? demand)
    {
        _demand = demand;
    }

    public bool IsValid => _demand?.IsActive == true;

    public bool IsFor(ListItemViewModel item) => IsValid && ReferenceEquals(_demand!.Item, item);

    public void Release() => _demand?.Release();
}
