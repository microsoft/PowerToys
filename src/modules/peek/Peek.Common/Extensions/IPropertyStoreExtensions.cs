// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Peek.Common.Models;

namespace Peek.Common.Extensions
{
    public static class IPropertyStoreExtensions
    {
        /// <summary>
        /// Helper method that retrieves a uint value from the given property store.
        /// Returns 0 if the value is not a VT_UI4 (4-byte unsigned integer in little-endian order).
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The uint value</returns>
        public static uint? TryGetUInt(this DisposablePropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                return null;
            }

            try
            {
                propertyStore.GetValue(ref key, out PropVariant propVar);

                // VT_UI4 Indicates a 4-byte unsigned integer formatted in little-endian byte order.
                return (VarEnum)propVar.Vt == VarEnum.VT_UI4 ? propVar.UlVal : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method that retrieves a ulong value from the given property store.
        /// Returns 0 if the value is not a VT_UI8 (8-byte unsigned integer in little-endian order).
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">the pkey</param>
        /// <returns>the ulong value</returns>
        public static ulong? TryGetULong(this DisposablePropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                return null;
            }

            try
            {
                propertyStore.GetValue(ref key, out PropVariant propVar);

                // VT_UI8 Indicates an 8-byte unsigned integer formatted in little-endian byte order.
                return (VarEnum)propVar.Vt == VarEnum.VT_UI8 ? propVar.UhVal : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method that retrieves a string value from the given property store.
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The string value</returns>
        public static string? TryGetString(this DisposablePropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                return null;
            }

            try
            {
                propertyStore.GetValue(ref key, out PropVariant propVar);

                return (VarEnum)propVar.Vt == VarEnum.VT_LPWSTR ? Marshal.PtrToStringUni(propVar.P) ?? string.Empty : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
