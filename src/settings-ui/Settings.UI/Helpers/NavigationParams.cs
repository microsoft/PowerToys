// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public class NavigationParams
{
    public string ElementName { get; set; }

    public string ParentElementName { get; set; }

    public NavigationParams(string elementName, string parentElementName = null)
    {
        ElementName = elementName;
        ParentElementName = parentElementName;
    }
}
