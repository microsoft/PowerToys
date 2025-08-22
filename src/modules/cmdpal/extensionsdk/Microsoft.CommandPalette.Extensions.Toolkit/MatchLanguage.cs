// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public enum MatchLanguage
{
    English,

    // CJK will be matched by the CJK language pack
    Chinese,
    Japanese,
    Korean,
}
