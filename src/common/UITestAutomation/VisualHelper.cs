// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    internal static class VisualHelper
    {
        /// <summary>
        /// Compare two pixels with a fuzz factor
        /// </summary>
        /// <param name="c1">base color</param>
        /// <param name="c2">test color</param>
        /// <param name="fuzz">fuzz factor, default is 10</param>
        /// <returns>true if same; otherwise, is false</returns>
        public static bool PixIsSame(Color c1, Color c2, int fuzz = 10)
        {
            return Math.Abs(c1.A - c2.A) <= fuzz && Math.Abs(c1.R - c2.R) <= fuzz && Math.Abs(c1.G - c2.G) <= fuzz && Math.Abs(c1.B - c2.B) <= fuzz;
        }
    }
}
