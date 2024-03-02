// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using FileActionsMenu.Helpers.Properties;

namespace FileActionsMenu.Helpers
{
    public class ResourceHelper
    {
        public static string GetResource(string key)
        {
            return Resource.ResourceManager.GetString(key, CultureInfo.CurrentCulture) ?? $"!!{key}!!";
        }
    }
}
