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
    internal class EmbeddingModel : IEmbeddingModel
    {
        public float[] ComputeEmbedding(string text)
        {
            // Placeholder for the actual implementation
            return text.Select(c => (float)c % 10).Take(10).ToArray();
        }

        public bool SupportsSearch => true;
    }
}
