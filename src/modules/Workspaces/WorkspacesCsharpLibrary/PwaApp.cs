// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkspacesCsharpLibrary
{
    public class PwaApp
    {
        public required string Name { get; set; }

        public required string IconFilename { get; set; }

        public required string AppId { get; set; }
    }
}
