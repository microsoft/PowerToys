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
    [Guid("AAB49DD5-AD0B-40AE-B654-AE8976BF6BD2")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CSearchCatalogManagerImp : ISearchCatalogManager
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern ISearchQueryHelper GetQueryHelper();
    }
}
