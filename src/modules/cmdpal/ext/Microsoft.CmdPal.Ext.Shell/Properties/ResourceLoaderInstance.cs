// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Shell;

internal static class ResourceLoaderInstance
{
    public static string GetString(string resourceKey)
    {
        return Properties.Resources.ResourceManager.GetString(resourceKey, Properties.Resources.Culture) ?? throw new InvalidOperationException($"Resource key '{resourceKey}' not found.");
    }
}
