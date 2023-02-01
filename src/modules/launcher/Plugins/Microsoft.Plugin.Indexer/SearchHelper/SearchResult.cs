// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin.Interfaces;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class SearchResult : IFileDropResult
    {
        // Contains the Path of the file or folder
        public string Path { get; set; }

        // Contains the Title of the file or folder
        public string Title { get; set; }
    }
}
