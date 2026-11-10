// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Accessibility > Text size, which Windows' own flyouts follow, and so do the notice and the choice window.</summary>
internal static class SystemTextScale
{
    /// <summary>Gets the text size as a scale, 1 for 100%, or 1 when Windows can't say.</summary>
    public static double Current()
    {
        try
        {
            return new global::Windows.UI.ViewManagement.UISettings().TextScaleFactor;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or TypeLoadException)
        {
            return 1;
        }
    }
}
