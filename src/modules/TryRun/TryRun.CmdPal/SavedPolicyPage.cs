// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.CmdPal;

internal sealed partial class SavedPolicyPage : ListPage
{
    private readonly string workerPath;
    private readonly PolicyProfileStore profiles;
    private readonly Dictionary<string, SavedPolicyFilePage> pages = new(StringComparer.Ordinal);
    private readonly object pagesLock = new();

    public SavedPolicyPage(string workerPath, PolicyProfileStore? profiles = null)
    {
        this.workerPath = workerPath;
        this.profiles = profiles ?? new PolicyProfileStore();
        Id = "com.microsoft.powertoys.tryrun.saved-policies";
        Name = "Choose policy";
        Title = "Try Run with a saved policy";
        Icon = new IconInfo("\uE72E");
    }

    public override IListItem[] GetItems()
    {
        lock (pagesLock)
        {
            try
            {
                var saved = profiles.List();
                var current = saved.Select(profile => profile.Id + ":" + profile.Revision).ToHashSet(StringComparer.Ordinal);
                var items = new List<IListItem>();
                foreach (var profile in saved)
                {
                    if (profile.Linux)
                    {
                        items.Add(new ListItem(new NoOpCommand()) { Title = profile.Name, Subtitle = "Linux policy: use the Try Run application. This entry supports Windows workloads." });
                        continue;
                    }

                    var key = profile.Id + ":" + profile.Revision;
                    if (!pages.TryGetValue(key, out var page))
                    {
                        page = new SavedPolicyFilePage(workerPath, profiles, profile);
                        pages.Add(key, page);
                    }

                    items.Add(new ListItem(page)
                    {
                        Title = profile.Name,
                        Subtitle = $"Windows · {profile.Id} · revision {profile.Revision[..12]}",
                    });
                }

                foreach (var cached in pages.ToArray())
                {
                    if (current.Contains(cached.Key))
                    {
                        continue;
                    }

                    if (cached.Value.IsLoading)
                    {
                        items.Add(ActiveRun(cached.Value));
                    }
                    else
                    {
                        pages.Remove(cached.Key);
                    }
                }

                if (items.Count == 0)
                {
                    items.Add(new ListItem(new NoOpCommand()) { Title = "No saved policies", Subtitle = "Configure and save a policy in Try Run, then return here" });
                }

                return items.ToArray();
            }
            catch (Exception exception)
            {
                return new IListItem[] { new ListItem(new NoOpCommand()) { Title = "Saved policies could not be loaded", Subtitle = exception.Message } }
                    .Concat(pages.Values.Where(page => page.IsLoading).Select(ActiveRun)).ToArray();
            }
        }
    }

    private static IListItem ActiveRun(SavedPolicyFilePage page) => new ListItem(page)
    {
        Title = "Active run · " + page.Title,
        Subtitle = "The saved profile changed or is unavailable. Open this run to inspect output or stop it.",
    };
}
