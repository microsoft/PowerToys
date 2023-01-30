// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Wox.Plugin.Interfaces;

namespace Microsoft.Plugin.Indexer.SearchHelper
{
    public class SearchResult : IFileDropResult
    {
        // Contains the Path of the file or folder
        public string Path { get; set; }

        // Contains the Title of the file or folder
        public string Title { get; set; }

        // Contains the Path of the file or folder in localized version
        public string PathLocalized { get; set; }

        // Contains the  Title of the file or folder in localized version
        public string TitleLocalized { get; set; }

        // Contains the Modified date
        public DateTime DateModified { get; set; }

        // String to compare the object instance: "<Title>:<Path>"
        // We have to compare the original filesystem values to be correct on translated items.
        public string CompareString => Title + ":" + Path;
    }
}
