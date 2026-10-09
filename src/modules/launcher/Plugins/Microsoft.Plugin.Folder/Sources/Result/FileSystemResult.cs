// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class FileSystemResult : List<DisplayFileInfo>
    {
        public FileSystemResult()
        {
        }

        public FileSystemResult(IEnumerable<DisplayFileInfo> collection)
            : base(collection)
        {
        }

        public FileSystemResult(int capacity)
            : base(capacity)
        {
        }

        public static FileSystemResult Error(Exception exception)
        {
            return new FileSystemResult { Exception = exception };
        }

        public Exception Exception { get; private set; }

        public bool HasException() => Exception != null;
    }
}
