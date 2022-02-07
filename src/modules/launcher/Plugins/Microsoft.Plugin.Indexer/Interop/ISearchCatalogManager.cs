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
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF50")]
    [ComConversionLoss]
    [CoClass(typeof(CSearchCatalogManagerImp))]
    [InterfaceType(1)]
    public interface ISearchCatalogManager
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.Interface)]
        ISearchQueryHelper GetQueryHelper();
    }
}
