// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("8327b13c-b63f-4b24-9b8a-d010dcc3f599")]
    internal interface IPreviewHandlerVisuals
    {
        void SetBackgroundColor(COLORREF color);

        void SetFont(ref LOGFONT plf);

        void SetTextColor(COLORREF color);
    }
}
