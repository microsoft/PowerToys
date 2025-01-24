// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Ext.WinGet.Pages;

public partial class InstalledPackagesPage : ListPage
{
    public InstalledPackagesPage()
    {
        Icon = new("\uE74C");
        Name = "Installed Packages";
        IsLoading = true;
    }

    internal async Task<PackageCatalog> GetLocalCatalog()
    {
        var catalogRef = WinGetStatics.Manager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
        var connectResult = await catalogRef.ConnectAsync();
        var compositeCatalog = connectResult.PackageCatalog;
        return compositeCatalog;
    }

    public override IListItem[] GetItems()
    {
        var fetchAsync = FetchLocalPackagesAsync();
        fetchAsync.ConfigureAwait(false);
        var results = fetchAsync.Result;
        IListItem[] listItems = !results.Any()
            ? [
                new ListItem(new NoOpCommand())
                    {
                        Title = "No packages found",
                    }
            ]
            : results.Select(p =>
            {
                var versionText = p.InstalledVersion?.Version ?? string.Empty;

                Tag[] tags = string.IsNullOrEmpty(versionText) ? [] : [new Tag() { Text = versionText }];
                return new ListItem(new NoOpCommand())
                {
                    Title = p.Name,
                    Subtitle = p.Id,
                    Tags = tags,
                };
            }).ToArray();
        IsLoading = false;
        return listItems;
    }

    private async Task<IEnumerable<CatalogPackage>> FetchLocalPackagesAsync()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        var results = new HashSet<CatalogPackage>(new PackageIdCompare());

        var catalog = await GetLocalCatalog();
        var opts = WinGetStatics.WinGetFactory.CreateFindPackagesOptions();
        var searchResults = await catalog.FindPackagesAsync(opts);
        foreach (var match in searchResults.Matches.ToArray())
        {
            // Print the packages
            var package = match.CatalogPackage;
            results.Add(package);
        }

        stopwatch.Stop();

        Debug.WriteLine($"Search took {stopwatch.ElapsedMilliseconds}");

        return results;
    }
}
