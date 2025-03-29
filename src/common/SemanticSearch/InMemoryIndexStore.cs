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
    public class InMemoryIndexStore : IIndexStore
    {
        private readonly List<(string Text, float[] Embedding)> _indexedData = new();

        public void AddData(List<(string Text, float[] Embedding)> data)
        {
            _indexedData.AddRange(data);
        }

        public List<(string Text, float[] Embedding)> GetData()
        {
            return _indexedData;
        }
    }
}
