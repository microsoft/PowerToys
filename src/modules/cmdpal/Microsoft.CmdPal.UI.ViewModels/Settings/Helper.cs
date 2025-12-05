// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public static class Helper
{
    private static readonly global::PowerToys.Interop.LayoutMapManaged LayoutMap = new();

    public static string GetKeyName(uint key)
    {
        return LayoutMap.GetKeyName(key);
    }

    public static uint GetKeyValue(string key)
    {
        return LayoutMap.GetKeyValue(key);
    }

    public static readonly uint VirtualKeyWindows = global::PowerToys.Interop.Constants.VK_WIN_BOTH;
}
