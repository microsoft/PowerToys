// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HostsUILib.Models
{
    /// <summary>
    /// Represents the parsed hosts file
    /// </summary>
    public class HostsData
    {
        /// <summary>
        /// Gets the parsed entries
        /// </summary>
        public ReadOnlyCollection<Entry> Entries { get; }

        /// <summary>
        /// Gets the lines that couldn't be parsed
        /// </summary>
        public string AdditionalLines { get; }

        /// <summary>
        /// Gets a value indicating whether some entries been splitted
        /// </summary>
        public bool SplittedEntries { get; }

        public HostsData(List<Entry> entries, string additionalLines, bool splittedEntries)
        {
            Entries = entries.AsReadOnly();
            AdditionalLines = additionalLines;
            SplittedEntries = splittedEntries;
        }
    }
}
