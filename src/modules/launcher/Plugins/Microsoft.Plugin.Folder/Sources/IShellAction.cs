// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Run.Plugin;

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IShellAction
    {
        bool Execute(string sanitizedPath, IPublicAPI contextApi);

        bool ExecuteSanitized(string search, IPublicAPI contextApi);
    }
}
