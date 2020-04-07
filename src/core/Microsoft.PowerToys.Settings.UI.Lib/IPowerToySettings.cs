// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public interface IPowerToySettings
    {
        string name { get; set; }

        string version { get; set; }

        string ToJsonString();
    }
}
