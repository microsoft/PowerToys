// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Microsoft.PowerToys.Telemetry;

// <summary>
//     Machine setup/switching implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.Core;

internal class MyRectangle
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}
