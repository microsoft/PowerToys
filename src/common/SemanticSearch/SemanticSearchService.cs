// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.SemanticSearch;

public abstract class SemanticSearchService
{
    private readonly IIndexStore _indexStore;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly ISimilarityCalculator _similarityCalculator;

    protected SemanticSearchService(IIndexStore indexStore, IEmbeddingModel embeddingModel, ISimilarityCalculator similarityCalculator)
    {
        _indexStore = indexStore;
        _embeddingModel = embeddingModel;
        _similarityCalculator = similarityCalculator;
    }

    public virtual void IndexData(List<string> data)
    {
        var indexedData = data.Select(text => (text, _embeddingModel.ComputeEmbedding(text))).ToList();
        _indexStore.AddData(indexedData);
    }

    public virtual List<string> Search(string query, int topK = 5, double threshold = 0.7)
    {
        if (!_embeddingModel.SupportsSearch)
        {
            return new List<string>();
        }

        var queryEmbedding = _embeddingModel.ComputeEmbedding(query);
        var indexedData = _indexStore.GetData();

        return indexedData
            .Select(entry => (entry.Text, similarity: _similarityCalculator.ComputeSimilarity(queryEmbedding, entry.Embedding)))
            .Where(entry => entry.similarity >= threshold)
            .OrderByDescending(entry => entry.similarity)
            .Take(topK)
            .Select(entry => entry.Text)
            .ToList();
    }
}
