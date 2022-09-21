// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Plugin.Folder.Sources
{
    internal class FolderLinksSettings : IFolderLinks
    {
        private readonly FolderSettings _settings;

        public FolderLinksSettings(FolderSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<FolderLink> FolderLinks()
        {
            return _settings.FolderLinks;
        }
    }
}
