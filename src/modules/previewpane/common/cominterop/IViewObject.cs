// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Enables an object to display itself directly without passing a data object to the caller.
    /// </summary>
    [ComImport]
    [Guid("0000010D-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IViewObject
    {
        /// <summary>
        /// Draws a representation of an object onto the specified device context.
        /// </summary>
        /// <param name="dwAspect">Specifies the aspect to be drawn, that is, how the object is to be represented.</param>
        /// <param name="lindex">Portion of the object that is of interest for the draw operation.</param>
        /// <param name="pvAspect">Pointer to additional information in a DVASPECTINFO structure that enables drawing optimizations depending on the aspect specified.</param>
        /// <param name="ptd">Pointer to the DVTARGETDEVICE structure that describes the device for which the object is to be rendered.</param>
        /// <param name="hdcTargetDev">Information context for the target device indicated by the ptd parameter from which the object can extract device metrics and test the device's capabilities.</param>
        /// <param name="hdcDraw">Device context on which to draw. For a windowless object, the hdcDraw parameter should be in MM_TEXT mapping mode with its logical coordinates matching the client coordinates of the containing window.</param>
        /// <param name="lprcBounds">Pointer to a RECTL structure specifying the rectangle on hdcDraw and in which the object should be drawn.</param>
        /// <param name="lprcWBounds">If hdcDraw is a metafile device context, pointer to a RECTL structure specifying the bounding rectangle in the underlying metafile.</param>
        /// <param name="pfnContinue">Pointer to a callback function that the view object should call periodically during a lengthy drawing operation to determine whether the operation should continue or be canceled.</param>
        /// <param name="dwContinue">Value to pass as a parameter to the function pointed to by the pfnContinue parameter.</param>
        void Draw([MarshalAs(UnmanagedType.U4)] uint dwAspect, int lindex, IntPtr pvAspect, [In] IntPtr ptd, IntPtr hdcTargetDev, IntPtr hdcDraw, [MarshalAs(UnmanagedType.Struct)] ref RECT lprcBounds, [In] IntPtr lprcWBounds, IntPtr pfnContinue, [MarshalAs(UnmanagedType.U4)] uint dwContinue);
    }
}
