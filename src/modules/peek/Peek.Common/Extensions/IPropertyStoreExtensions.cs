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
        /// <param name="key">The key</param>
        /// <returns>The uint value</returns>
        public static uint GetUInt(this IPropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                throw new ArgumentNullException("propertyStore");
            }

            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            // VT_UI4 Indicates a 4-byte unsigned integer formatted in little-endian byte order.
            if ((VarEnum)propVar.Vt == VarEnum.VT_UI4)
            {
                return propVar.UlVal;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Helper method that retrieves a ulong value from the given property store.
        /// Returns 0 if the value is not a VT_UI8 (8-byte unsigned integer in little-endian order).
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">the pkey</param>
        /// <returns>the ulong value</returns>
        public static ulong GetULong(this IPropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                throw new ArgumentNullException("propertyStore");
            }

            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            // VT_UI8 Indicates an 8-byte unsigned integer formatted in little-endian byte order.
            if ((VarEnum)propVar.Vt == VarEnum.VT_UI8)
            {
                return propVar.UhVal;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Helper method that retrieves a string value from the given property store.
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The string value</returns>
        public static string GetString(this IPropertyStore propertyStore, PropertyKey key)
        {
            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            if ((VarEnum)propVar.Vt == VarEnum.VT_LPWSTR)
            {
                return Marshal.PtrToStringUni(propVar.P) ?? string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Helper method that retrieves an array of string values from the given property store.
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The array of string values</returns>
        public static string[] GetStringArray(this IPropertyStore propertyStore, PropertyKey key)
        {
            PropVariant propVar;
            propertyStore.GetValue(ref key, out propVar);

            List<string> values = new List<string>();

            if ((VarEnum)propVar.Vt == (VarEnum.VT_LPWSTR | VarEnum.VT_VECTOR))
            {
                for (int elementIndex = 0; elementIndex < propVar.Calpwstr.CElems; elementIndex++)
                {
                    var stringVal = Marshal.PtrToStringUni(Marshal.ReadIntPtr(propVar.Calpwstr.PElems, elementIndex));
                    if (stringVal != null)
                    {
                        values.Add(stringVal);
                    }
                }
            }

            return values.ToArray();
        }
    }
}
