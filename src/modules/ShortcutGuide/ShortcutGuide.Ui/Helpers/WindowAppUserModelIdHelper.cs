// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Reads the AppUserModelID of a top-level window.
    /// </summary>
    /// <remarks>
    /// Browsers host installed web apps (PWAs) in the browser process, so the process name alone
    /// cannot tell them apart. Chromium-based browsers assign every web app window its own
    /// AppUserModelID (for example <c>Chrome._crx_&lt;app id&gt;</c>), which the taskbar uses to
    /// group them. This helper exposes that per-window identity.
    /// </remarks>
    internal static class WindowAppUserModelIdHelper
    {
        // PKEY_AppUserModel_ID
        private static readonly NativeMethods.PROPERTYKEY AppUserModelIdKey = new()
        {
            FmtId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            PId = 5,
        };

        private static readonly Guid PropertyStoreIid = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

        private static readonly StrategyBasedComWrappers ComWrappers = new();

        /// <summary>
        /// Gets the AppUserModelID explicitly assigned to a window.
        /// </summary>
        /// <param name="windowHandle">The window handle.</param>
        /// <returns>The AppUserModelID, or <c>null</c> when the window has none or it cannot be read.</returns>
        public static string? GetAppUserModelId(nint windowHandle)
        {
            if (windowHandle == 0 ||
                NativeMethods.SHGetPropertyStoreForWindow(windowHandle, in PropertyStoreIid, out nint propertyStorePointer) < 0 ||
                propertyStorePointer == 0)
            {
                return null;
            }

            try
            {
                var propertyStore = (IPropertyStore)ComWrappers.GetOrCreateObjectForComInstance(propertyStorePointer, CreateObjectFlags.UniqueInstance);
                try
                {
                    if (propertyStore.GetValue(in AppUserModelIdKey, out NativeMethods.PROPVARIANT value) < 0)
                    {
                        return null;
                    }

                    try
                    {
                        return (VarEnum)value.Vt == VarEnum.VT_LPWSTR ? Marshal.PtrToStringUni(value.Value) : null;
                    }
                    finally
                    {
                        _ = NativeMethods.PropVariantClear(ref value);
                    }
                }
                finally
                {
                    ((ComObject)(object)propertyStore).FinalRelease();
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                return null;
            }
            finally
            {
                Marshal.Release(propertyStorePointer);
            }
        }
    }
}
