// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.PowerToys.Components;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.PowerToys
{
    public class Main : IPlugin, IContextMenu, IDisposable
    {
        private UtilityProvider _utilityProvider;
        private bool _disposed;

        public string Name => "PowerToys";

        public string Description => "Open PowerToys utilities and settings.";

        public void Init(PluginInitContext context)
        {
            _utilityProvider = new UtilityProvider();
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return selectedResult.ContextData is Utility u
                ? u.CreateContextMenuResults()
                : new List<ContextMenuResult>();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            foreach (var utility in _utilityProvider.GetEnabledUtilities())
            {
                var matchResult = StringMatcher.FuzzySearch(query.Search, utility.Name);
                if (string.IsNullOrWhiteSpace(query.Search) || matchResult.Score > 0)
                {
                    results.Add(utility.CreateResult(matchResult));
                }
            }

            return results.OrderBy(r => r.Title).ToList();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _utilityProvider?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
