// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public sealed class ReleaseNotesItem
    {
        public string Title { get; set; }

        public string Markdown { get; set; }

        public DateTimeOffset PublishedDate { get; set; }

        public string VersionGroup { get; set; }

        public string HeaderImageUri { get; set; }
    }
}
