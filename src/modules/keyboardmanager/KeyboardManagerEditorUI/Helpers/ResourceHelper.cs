// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.ApplicationModel.Resources;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class ResourceHelper
    {
        private static ResourceLoader? _resourceLoader;

        public static string GetString(string resourceKey)
        {
            _resourceLoader ??= new ResourceLoader();
            return _resourceLoader.GetString(resourceKey);
        }
    }
}
