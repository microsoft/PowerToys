// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public class DefaultSearch : SearchBase
    {
        public DefaultSearch(ResultCreator resultCreator, OneNoteSettings settings)
            : base(string.Empty, resultCreator, settings)
        {
        }

        public override List<Result> GetResults(string search, bool showSingleResults)
        {
            // Check for invalid start of query i.e. symbols
            if (!char.IsLetterOrDigit(search[0]))
            {
                return ResultCreator.InvalidQuery(showSingleResults);
            }

            var results = OneNoteApplication.FindPages(search)
                                            .Select(pg => ResultCreator.CreatePageResult(pg, search))
                                            .ToList();

            return results.Count != 0 ? results : ResultCreator.NoMatchesFound(showSingleResults);
        }
    }
}
