// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComImport]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
    [CoClass(typeof(CSearchQueryHelperImp))]
    [InterfaceType(1)]
    public interface ISearchQueryHelper
    {
        [DispId(1610678272)]
        string ConnectionString
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
        }

        [DispId(1610678273)]
        uint QueryContentLocale
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [DispId(1610678275)]
        uint QueryKeywordLocale
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [DispId(1610678281)]
        string QueryContentProperties
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            [param: MarshalAs(UnmanagedType.LPWStr)]
            set;
        }

        [DispId(1610678283)]
        string QuerySelectColumns
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            [param: MarshalAs(UnmanagedType.LPWStr)]
            set;
        }

        [DispId(1610678285)]
        string QueryWhereRestrictions
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            [param: MarshalAs(UnmanagedType.LPWStr)]
            set;
        }

        [DispId(1610678287)]
        string QuerySorting
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            [param: MarshalAs(UnmanagedType.LPWStr)]
            set;
        }

        [DispId(1610678291)]
        int QueryMaxResults
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GenerateSQLFromUserQuery([In][MarshalAs(UnmanagedType.LPWStr)] string pszQuery);
    }
}
