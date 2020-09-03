// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Plugin.Indexer.SearchHelper;

namespace Microsoft.Plugin.Indexer
{
    public interface ISearch
    {
        List<OleDBResult> Query(string connectionString, string sqlQuery);
    }
}
