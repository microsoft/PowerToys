// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.SemanticSearch
{
    public interface ISemanticSearchService
    {
        void IndexData(ModuleName moduleName, List<string> data);

        List<string> Search(ModuleName moduleName, string query, int topK = 5, double threshold = 0.7);
    }
}
