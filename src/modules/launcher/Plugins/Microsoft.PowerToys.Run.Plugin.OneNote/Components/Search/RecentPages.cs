// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LinqToOneNote;
using Wox.Plugin;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public class RecentPages : SearchBase
    {
        public RecentPages(ResultCreator resultCreator, OneNoteSettings settings)
            : base(Keywords.RecentPages, resultCreator, settings)
        {
        }

        public override List<Result> GetResults(string search, bool showSingleResults)
        {
            int count = 25; // TODO: Ideally this should match PowerToysRunSettings.MaxResultsToShow
            /*          var settingsUtils = new SettingsUtils();
                        var generalSettings = settingsUtils.GetSettings<GeneralSettings>();*/
            if (search.Length > Keyword.Length && int.TryParse(search[Keyword.Length..], out int userChosenCount))
            {
                count = userChosenCount;
            }

            return OneNoteApplication.GetFullHierarchy()
                                     .Notebooks
                                     .GetAllPages()
                                     .Where(SettingsCheck)
                                     .OrderByDescending(pg => pg.LastModified)
                                     .Take(count)
                                     .Select(ResultCreator.CreateRecentPageResult)
                                     .ToList();
        }
    }
}
