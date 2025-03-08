// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using FileActionsMenu.Helpers.Properties;

namespace FileActionsMenu.Helpers
{
    public static class ResourceHelper
    {
        /// <summary>
        /// Gets a resource string from the resource file.
        /// If the resource is not found, it returns a string with the resource name surrounded by '!!'.
        /// </summary>
        /// <param name="key">The name of the resource.</param>
        /// <returns>The requested resource.</returns>
        public static string GetResource(string key)
        {
            return Resource.ResourceManager.GetString(key, CultureInfo.CurrentCulture) ?? $"!!{key}!!";
        }
    }
}
