// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.Folder
{
    using Wox.Plugin.Interfaces;

    public class SearchResult : IFileDropResult
    {
        public string Path { get; set; }

        public ResultType Type { get; set; }
    }
}
