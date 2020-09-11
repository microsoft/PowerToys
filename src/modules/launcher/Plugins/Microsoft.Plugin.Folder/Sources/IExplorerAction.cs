// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IExplorerAction
    {
        bool Execute(string sanitizedPath);

        bool ExecuteSanitized(string search);

        bool ExecuteWithCatch(string filePath);

        bool ExecuteSanitizedWithCatch(string filePath);
    }
}
