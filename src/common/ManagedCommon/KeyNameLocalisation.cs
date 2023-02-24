// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon.Properties;

namespace ManagedCommon
{
    [ComVisible(true)]
    public static class KeyNameLocalisation
    {
        /// <summary>
        /// Gets the localisation of a keyboard key
        /// </summary>
        /// <param name="keyName">The name of the key</param>
        /// <returns>The localised key. If none gets found it just returns <paramref name="keyName"/>.</returns>
        [ComVisible(true)]
        public static string GetLocalisation(string keyName)
        {
            return Resources.ResourceManager.GetString("Keyboard_" + keyName, CultureInfo.CurrentUICulture) ?? keyName;
        }
    }
}
