// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkspacesEditor.Models
{
    internal static class ApplicationWindowMatcher
    {
        internal static IEnumerable<(Application Updated, Application Previous)> Match(
            IEnumerable<Application> previousApplications,
            IEnumerable<Application> updatedApplications)
        {
            var previousById = previousApplications.Where(app => !string.IsNullOrEmpty(app.Id))
                .ToLookup(app => app.IdWithoutBraces, StringComparer.OrdinalIgnoreCase);
            foreach (var updated in updatedApplications.GroupBy(app => app.IdWithoutBraces, StringComparer.OrdinalIgnoreCase))
            {
                var previous = previousById[updated.Key];
                if (!previous.Any() || (previous.Count() == 1 && updated.Count() == 1))
                {
                    var match = previous.FirstOrDefault();
                    foreach (var app in updated)
                    {
                        yield return (app, match);
                    }

                    continue;
                }

                foreach (var pair in MatchDuplicateStamp(previous, updated))
                {
                    yield return pair;
                }
            }
        }

        private static IEnumerable<(Application Updated, Application Previous)> MatchDuplicateStamp(
            IEnumerable<Application> previous, IEnumerable<Application> updated)
        {
            var matches = new Dictionary<Application, Application>();
            var used = new HashSet<Application>();

            void PairBy<TKey>(Func<Application, TKey> key, bool unique, Func<Application, bool> eligible)
            {
                var oldGroups = previous.Where(app => !used.Contains(app) && eligible(app)).ToLookup(key);
                var newGroups = updated.Where(app => !matches.ContainsKey(app) && eligible(app)).ToLookup(key);
                foreach (var newGroup in newGroups)
                {
                    var oldGroup = oldGroups[newGroup.Key];
                    if (unique && (newGroup.Count() != 1 || oldGroup.Count() != 1))
                    {
                        continue;
                    }

                    foreach (var (current, old) in newGroup.Zip(oldGroup))
                    {
                        matches.Add(current, old);
                        used.Add(old);
                    }
                }
            }

            // Windows from repeated launches can carry the same stamp. Match the entire group before
            // handing its old identity or settings to a window. Only indistinguishable rows use order.
            PairBy(app => (ProgramIdentity(app), app.AppTitle ?? string.Empty, app.MonitorNumber, app.Position, app.Minimized, app.Maximized), unique: false, _ => true);
            PairBy(app => (ProgramIdentity(app), app.AppTitle), unique: true, app => !string.IsNullOrEmpty(app.AppTitle));
            PairBy(app => (ProgramIdentity(app), app.MonitorNumber, app.Position), unique: true, app => app.Position.Width > 0 && app.Position.Height > 0);
            PairBy(ProgramIdentity, unique: true, app => !string.IsNullOrEmpty(app.AppPath) || !string.IsNullOrEmpty(app.AppUserModelId));

            // Recapture keeps the stamp on a matched window ahead of any unmatched ones.
            foreach (var pair in matches)
            {
                yield return (pair.Key, pair.Value);
            }

            foreach (var app in updated.Where(app => !matches.ContainsKey(app)))
            {
                yield return (app, null);
            }
        }

        private static (string Program, string Pwa) ProgramIdentity(Application app)
        {
            string program = string.IsNullOrEmpty(app.AppUserModelId) ? app.AppPath : app.AppUserModelId;
            return (program?.ToUpperInvariant() ?? string.Empty, app.PwaAppId?.ToUpperInvariant() ?? string.Empty);
        }
    }
}
