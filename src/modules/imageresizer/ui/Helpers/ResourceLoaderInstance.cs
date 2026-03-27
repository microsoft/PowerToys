// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Windows.ApplicationModel.Resources;

namespace ImageResizer.Helpers
{
    internal static class ResourceLoaderInstance
    {
        private static Func<string, string> _getString;

        internal static Func<string, string> GetString
        {
            get => _getString ??= CreateDefault();
            set => _getString = value;
        }

        private static Func<string, string> CreateDefault()
        {
            var loader = new ResourceLoader("PowerToys.ImageResizer.pri");
            return loader.GetString;
        }
    }
}
