// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Management.Automation;
using System.Management.Automation.Subsystem.Feedback;

namespace WinGetCommandNotFound
{
    public sealed class WinGetCommandNotFoundFeedbackPredictor : IFeedbackProvider
    {
        private readonly Guid _guid;

        private static readonly byte _maxSuggestions = 5;

        public static WinGetCommandNotFoundFeedbackPredictor Singleton { get; } = new WinGetCommandNotFoundFeedbackPredictor(Init.Id);

        private WinGetCommandNotFoundFeedbackPredictor(string guid)
        {
            _guid = new Guid(guid);
        }

        public void Dispose()
        {
        }

        public Guid Id => _guid;

        public string Name => "Windows Package Manager - WinGet";

        public string Description => "Finds missing commands that can be installed via WinGet.";

        /// <summary>
        /// Gets feedback based on the given commandline and error record.
        /// </summary>
        public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
        {
            var lastError = context.LastError;
            if (lastError is not null && lastError.FullyQualifiedErrorId == "CommandNotFoundException")
            {
                var target = (string)lastError.TargetObject;
                bool tooManySuggestions = false;
                string packageMatchFilterField = "command";
                var pkgList = FindPackages(target, ref tooManySuggestions, ref packageMatchFilterField);
                if (pkgList.Count == 0)
                {
                    return null;
                }

                // Build list of suggestions
                var suggestionList = new List<string>();
                foreach (var pkg in pkgList)
                {
                    suggestionList.Add(string.Format("winget install --id {0}", pkg.Members["Id"].Value.ToString()));
                }

                // Build footer message
                var footerMessage = tooManySuggestions ?
                    string.Format("Additional results can be found using \"winget search --{0} {1}\"", packageMatchFilterField, target) :
                    null;

                return new FeedbackItem(
                    "Try installing this package using winget:",
                    suggestionList,
                    footerMessage,
                    FeedbackDisplayLayout.Portrait);
            }

            return null;
        }

        // TODO CARLOS: when searching for "vim", I get no results. But typing out the cmdlet _does_ give me results (for name and moniker)
        private List<PSObject> FindPackages(string query, ref bool tooManySuggestions, ref string packageMatchFilterField)
        {
            var ps = PowerShell.Create(RunspaceMode.NewRunspace);

            // 1) Search by command
            var pkgList = ps.AddCommand("Find-WinGetPackage")
                .AddParameter("Command", query)
                .AddParameter("MatchOption", "StartsWithCaseInsensitive")
                .AddParameter("Count", _maxSuggestions)
                .AddParameter("Source", "winget")
                .Invoke().ToList();
            if (pkgList.Count > 0)
            {
                tooManySuggestions = pkgList.Count > _maxSuggestions;
                packageMatchFilterField = "command";
                return pkgList;
            }

            // 2) No matches found,
            //    search by name
            pkgList = ps.AddCommand("Find-WinGetPackage")
                .AddParameter("Name", query)
                .AddParameter("MatchOption", "ContainsCaseInsensitive")
                .AddParameter("Count", _maxSuggestions)
                .AddParameter("Source", "winget")
                .Invoke().ToList();
            if (pkgList.Count > 0)
            {
                tooManySuggestions = pkgList.Count > _maxSuggestions;
                packageMatchFilterField = "name";
                return pkgList;
            }

            // 3) No matches found,
            //    search by moniker
            pkgList = ps.AddCommand("Find-WinGetPackage")
                .AddParameter("Moniker", query)
                .AddParameter("MatchOption", "ContainsCaseInsensitive")
                .AddParameter("Count", _maxSuggestions)
                .AddParameter("Source", "winget")
                .Invoke().ToList();
            tooManySuggestions = pkgList.Count > _maxSuggestions;
            packageMatchFilterField = "moniker";
            return pkgList;
        }
    }
}
