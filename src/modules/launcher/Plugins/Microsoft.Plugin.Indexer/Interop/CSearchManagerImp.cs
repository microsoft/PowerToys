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
    [Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39")]
    [TypeLibType(2)]
    [ClassInterface(ClassInterfaceType.None)]
    [ComConversionLoss]
    public class CSearchManagerImp : ISearchManager
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern ISearchCatalogManager GetCatalog([In][MarshalAs(UnmanagedType.LPWStr)] string pszCatalog);
    }
}
