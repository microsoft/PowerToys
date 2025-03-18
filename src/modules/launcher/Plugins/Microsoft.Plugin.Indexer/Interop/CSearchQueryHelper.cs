﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF63")]
    [CoClass(typeof(CSearchQueryHelperClass))]
    [ComImport]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1715:Identifiers should have correct prefix", Justification = "Using original name from type lib")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302:Interface names should begin with I", Justification = "Using original name from type lib")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Using original name from type lib")]
    public interface CSearchQueryHelper : ISearchQueryHelper
    {
    }
}
