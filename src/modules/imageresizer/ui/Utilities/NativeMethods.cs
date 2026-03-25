// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1636:FileHeaderCopyrightTextMustMatch", Justification = "File created under PowerToys.")]

namespace ImageResizer.Utilities
{
    internal class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);
    }
}
