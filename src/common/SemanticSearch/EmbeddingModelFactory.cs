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
    public static class EmbeddingModelFactory
    {
        private static IEmbeddingModel? _currentModel;
        private static string? _currentModelName;

        public static IEmbeddingModel? GetEmbeddingModel()
        {
            CheckAndUpdateModel();
            return _currentModel;
        }

        public static void CheckAndUpdateModel()
        {
            string configuredModelName = GetConfiguredModelName();
            if (_currentModelName == configuredModelName)
            {
                return;
            }

            _currentModel = configuredModelName switch
            {
                "Mock" => new EmbeddingModel(),
                _ => new EmbeddingModel(),
            };
            _currentModelName = configuredModelName;
        }

        private static string GetConfiguredModelName()
        {
            return "Mock";
        }
    }
}
