// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class ResourceLoaderInstance
{
    internal static ResourceLoader ResourceLoader { get; private set; }

    static ResourceLoaderInstance()
    {
        ResourceLoader = new ResourceLoader("resources.pri");
    }

    internal static string GetString(string resourceId)
    {
        try
        {
            return ResourceLoader.GetString(resourceId);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get string resourceId='{resourceId}'", ex);
            return $"!!{resourceId}!!";
        }
    }
}
