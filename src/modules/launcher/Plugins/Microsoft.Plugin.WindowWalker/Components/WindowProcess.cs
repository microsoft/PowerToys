// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents the process data of an open window in the process cache
    /// </summary>
    public class WindowProcess
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowProcess"/> class.
        /// </summary>
        public WindowProcess(uint id, string name, bool elevated)
        {
            ID = id;
            Name = name;
            IsElevated = elevated;
        }

        /// <summary>
        /// Gets or sets the id of the process
        /// </summary>
        public uint ID { get; set; }

        /// <summary>
        /// Gets or sets the name of the process
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the proces runs elevated or not
        /// </summary>
        public bool IsElevated { get; set; }
    }
}
