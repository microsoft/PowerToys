// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    public class Custom : Element
    {
        private static readonly string ExpectedControlType = "ControlType.Custom";

        /// <summary>
        /// Initializes a new instance of the <see cref="Custom"/> class.
        /// </summary>
        public Custom()
        {
            this.TargetControlType = Custom.ExpectedControlType;
        }
    }
}
