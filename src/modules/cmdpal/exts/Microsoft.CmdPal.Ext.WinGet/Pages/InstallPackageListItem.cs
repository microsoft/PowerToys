// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
            var heroIcon = new IconInfo(string.Empty);
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
            { Properties.Resources.winget_author, (metadata.Author, string.Empty) },
            { Properties.Resources.winget_publisher, (metadata.Publisher, metadata.PublisherUrl) },
            { Properties.Resources.winget_copyright, (metadata.Copyright, metadata.CopyrightUrl) },
            { Properties.Resources.winget_license, (metadata.License, metadata.LicenseUrl) },
            { Properties.Resources.winget_release_notes, (metadata.ReleaseNotes, string.Empty) },

            // The link to the release notes will only show up if there is an
            // actual URL for the release notes
            { Properties.Resources.winget_view_release_notes, (string.IsNullOrEmpty(metadata.ReleaseNotesUrl) ? string.Empty : Properties.Resources.winget_view_online, metadata.ReleaseNotesUrl) },
            { Properties.Resources.winget_publisher_support, (string.Empty, metadata.PublisherSupportUrl) },
        };
        var docs = metadata.Documentations.ToArray();
        foreach (var item in docs)
        {
            simpleData.Add(item.DocumentLabel, (string.Empty, item.DocumentUrl));
        }

        var options = default(UriCreationOptions);
        foreach (var kv in simpleData)
        {
            var text = string.IsNullOrEmpty(kv.Value.Item1) ? kv.Value.Item2 : kv.Value.Item1;
            var target = kv.Value.Item2;
            if (!string.IsNullOrEmpty(text))
            {
                Uri? uri = null;
                Uri.TryCreate(target, options, out uri);

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

        // might be an uninstall command
        var installCommand = new InstallPackageCommand(_package, isInstalled);

        if (isInstalled)
        {
            this.Icon = InstallPackageCommand.CompletedIcon;
            this.Command = new NoOpCommand();
            List<IContextItem> contextMenu = [];
            var uninstallContextItem = new CommandContextItem(installCommand)
            {
                IsCritical = true,
                Icon = InstallPackageCommand.DeleteIcon,
            };

            if (WinGetStatics.AppSearchCallback != null)
            {
                var callback = WinGetStatics.AppSearchCallback;
                var installedApp = callback(_package.DefaultInstallVersion.DisplayName);
                if (installedApp != null)
                {
                    this.Command = installedApp.Command;
                    contextMenu = [.. installedApp.MoreCommands];
                }
            }

            contextMenu.Add(uninstallContextItem);
            this.MoreCommands = contextMenu.ToArray();
            return;
        }

        // didn't find the app
        _installCommand = new InstallPackageCommand(_package, isInstalled);
        this.Command = _installCommand;

        Icon = _installCommand.Icon;
        _installCommand.InstallStateChanged += InstallStateChangedHandler;
    }

    private void InstallStateChangedHandler(object? sender, InstallPackageCommand e)
    {
        if (!ApiInformation.IsApiContractPresent("Microsoft.Management.Deployment", 12))
        {
            Logger.LogError($"RefreshPackageCatalogAsync isn't available");
            e.FakeChangeStatus();
            Command = e;
            Icon = (IconInfo?)Command.Icon;
            return;
        }

        _ = Task.Run(() =>
        {
            Stopwatch s = new();
            Logger.LogDebug($"Starting RefreshPackageCatalogAsync");
            s.Start();
            var refs = WinGetStatics.AvailableCatalogs.ToArray();

            foreach (var catalog in refs)
            {
                var operation = catalog.RefreshPackageCatalogAsync();
                operation.Wait();
            }

            s.Stop();
            Logger.LogDebug($"RefreshPackageCatalogAsync took {s.ElapsedMilliseconds}ms");
        }).ContinueWith((previous) =>
        {
            if (previous.IsCompletedSuccessfully)
            {
                Logger.LogDebug($"Updating InstalledStatus");
                UpdatedInstalledStatus();
            }
        });
    }
}
