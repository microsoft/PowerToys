// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Uri.Interfaces;

namespace Microsoft.Plugin.Uri.UriHelper
{
    public class UriResolver : IUrlResolver
    {
        public bool IsValidHost(System.Uri uri)
        {
            return true;
        }
    }
}
