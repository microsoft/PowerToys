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
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("B271E955-09E1-42E1-9B95-5994A534B613")]
    public class CSearchQueryHelperImp : ISearchQueryHelper
    {
        [DispId(1610678272)]
        public extern string ConnectionString
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            get;
        }

        [DispId(1610678273)]
        public extern uint QueryContentLocale
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [DispId(1610678275)]
        public extern uint QueryKeywordLocale
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [DispId(1610678281)]
        public extern string QueryContentProperties
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
        public extern string QuerySelectColumns
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
        public extern string QueryWhereRestrictions
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
        public extern string QuerySorting
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
        public extern int QueryMaxResults
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        public extern string GenerateSQLFromUserQuery([In][MarshalAs(UnmanagedType.LPWStr)] string pszQuery);
    }
}
