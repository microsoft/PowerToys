// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutGuide.Models
{
    internal sealed class ShortcutPageNavParam
    {
        public string AppName { get; set; } = string.Empty;

        public ShortcutFile ShortcutFile { get; set; }

        public int PageIndex { get; set; }
    }
}
