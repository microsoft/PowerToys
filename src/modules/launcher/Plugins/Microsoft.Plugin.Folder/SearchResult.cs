// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin.Interfaces;

namespace Microsoft.Plugin.Folder
{
    public class SearchResult : IFileDropResult
    {
        public string Path { get; set; }

        public ResultType Type { get; set; }
    }
}
