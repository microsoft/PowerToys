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
    [ComConversionLoss]
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69")]
    [InterfaceType(1)]
    public interface ISearchManager
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.Interface)]
        ISearchCatalogManager GetCatalog([In][MarshalAs(UnmanagedType.LPWStr)] string pszCatalog);
    }
}
