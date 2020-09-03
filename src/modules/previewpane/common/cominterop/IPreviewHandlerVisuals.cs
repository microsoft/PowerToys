// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Exposes methods for applying color and font information to preview handlers.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("8327b13c-b63f-4b24-9b8a-d010dcc3f599")]
    public interface IPreviewHandlerVisuals
    {
        /// <summary>
        /// Sets the background color of the preview handler.
        /// </summary>
        /// <param name="color">A value of type <see cref="COLORREF"/> to use for the preview handler background.</param>
        void SetBackgroundColor(COLORREF color);

        /// <summary>
        /// Sets the font attributes to be used for text within the preview handler.
        /// </summary>
        /// <param name="plf">A pointer to a <see cref="LOGFONT"/> Structure containing the necessary attributes for the font to use.</param>
        void SetFont(ref LOGFONT plf);

        /// <summary>
        /// Sets the color of the text within the preview handler.
        /// </summary>
        /// <param name="color">A value of type <see cref="COLORREF"/> to use for the preview handler text color.</param>
        void SetTextColor(COLORREF color);
    }
}
