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
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Ext.WinGet.Pages;

public partial class InstallPackageListItem : ListItem
{
    private readonly CatalogPackage _package;
    private InstallPackageCommand? _installCommand;

    public InstallPackageListItem(CatalogPackage package)
        : base(new NoOpCommand())
    {
        _package = package;

        var version = _package.DefaultInstallVersion;
        var versionText = version.Version;
        var versionTagText = versionText == "Unknown" && version.PackageCatalog.Info.Id == "StoreEdgeFD" ? "msstore" : versionText;

        Title = _package.Name;
        Subtitle = _package.Id;
        Tags = [new Tag() { Text = versionTagText }];

        var metadata = version.GetCatalogPackageMetadata();
        if (metadata != null)
        {
            var description = string.IsNullOrEmpty(metadata.Description) ? metadata.ShortDescription : metadata.Description;
            var detailsBody = $"""

{description}
""";
            IconInfo heroIcon = new(string.Empty);
            var icons = metadata.Icons;
            if (icons.Count > 0)
            {
                // There's also a .Theme property we could probably use to
                // switch between default or individual icons.
                heroIcon = new IconInfo(icons[0].Url);
            }

            Details = new Details()
            {
                Body = detailsBody,
                Title = metadata.PackageName,
                HeroImage = heroIcon,
                Metadata = GetDetailsMetadata(metadata).ToArray(),
            };
        }

        _ = Task.Run(UpdatedInstalledStatus);
    }

    private List<IDetailsElement> GetDetailsMetadata(CatalogPackageMetadata metadata)
    {
        List<IDetailsElement> detailsElements = [];

        // key -> {text, url}
        Dictionary<string, (string, string)> simpleData = new()
        {
            { "Author", (metadata.Author, string.Empty) },
            { "Publisher", (metadata.Publisher, metadata.PublisherUrl) },
            { "Copyright", (metadata.Copyright, metadata.CopyrightUrl) },
            { "License", (metadata.License, metadata.LicenseUrl) },
            { "Release Notes", (metadata.ReleaseNotes, string.Empty) },

            // The link to the release notes will only show up if there is an
            // actual URL for the release notes
            { "View Release Notes", (string.IsNullOrEmpty(metadata.ReleaseNotesUrl) ? string.Empty : "View online", metadata.ReleaseNotesUrl) },
            { "Publisher Support", (string.Empty, metadata.PublisherSupportUrl) },
        };
        var docs = metadata.Documentations.ToArray();
        foreach (var item in docs)
        {
            simpleData.Add(item.DocumentLabel, (string.Empty, item.DocumentUrl));
        }

        foreach (var kv in simpleData)
        {
            var text = string.IsNullOrEmpty(kv.Value.Item1) ? kv.Value.Item2 : kv.Value.Item1;
            var target = kv.Value.Item2;
            if (!string.IsNullOrEmpty(text))
            {
                Uri? uri = null;
                try
                {
                    uri = new Uri(target);
                }
                catch (System.UriFormatException)
                {
                }

                var pair = new DetailsElement()
                {
                    Key = kv.Key,
                    Data = new DetailsLink() { Link = uri, Text = text },
                };
                detailsElements.Add(pair);
            }
        }

        if (metadata.Tags.Any())
        {
            var pair = new DetailsElement()
            {
                Key = "Tags",
                Data = new DetailsTags() { Tags = metadata.Tags.Select(t => new Tag(t)).ToArray() },
            };
            detailsElements.Add(pair);
        }

        return detailsElements;
    }

    private async void UpdatedInstalledStatus()
    {
        var status = await _package.CheckInstalledStatusAsync();
        var isInstalled = _package.InstalledVersion != null;
        _installCommand = new InstallPackageCommand(_package, isInstalled);
        this.Command = _installCommand;
        Icon = _installCommand.Icon;

        _installCommand.InstallStateChanged += InstallStateChangedHandler;
    }

    private void InstallStateChangedHandler(object? sender, InstallPackageCommand e)
    {
        if (!ApiInformation.IsApiContractPresent("Microsoft.Management.Deployment", 12))
        {
            Debug.WriteLine($"RefreshPackageCatalogAsync isn't available");
            e.FakeChangeStatus();
            Command = e;
            Icon = Command.Icon;
            return;
        }

        _ = Task.Run(() =>
        {
            Stopwatch s = new();
            Debug.WriteLine($"Starting RefreshPackageCatalogAsync");
            s.Start();
            var refs = WinGetStatics.AvailableCatalogs.ToArray();

            foreach (var catalog in refs)
            {
                var operation = catalog.RefreshPackageCatalogAsync();
                operation.Wait();
            }

            s.Stop();
            Debug.WriteLine($"  RefreshPackageCatalogAsync took {s.ElapsedMilliseconds}ms");
        }).ContinueWith((previous) =>
        {
            if (previous.IsCompletedSuccessfully)
            {
                Debug.WriteLine($"Updating InstalledStatus");
                UpdatedInstalledStatus();
            }
        });
    }
}
