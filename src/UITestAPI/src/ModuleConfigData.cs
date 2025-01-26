// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.UITests.API
{
    public enum PowerToysModule
    {
        None,
        Fancyzone,
        KeyboardManagerKeys,
        KeyboardManagerShortcuts,
        Hosts,
    }

    public struct ModuleConfigData(string moduleName, string windowName)
    {
        public string ModuleName = moduleName;
        public string WindowName = windowName;
    }
}
