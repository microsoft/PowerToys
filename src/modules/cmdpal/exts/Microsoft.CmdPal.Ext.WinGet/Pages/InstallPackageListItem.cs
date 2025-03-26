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

    // Lazy-init the details
    private readonly Lazy<Details?> _details;

    public override IDetails? Details { get => _details.Value; set => base.Details = value; }

    private InstallPackageCommand? _installCommand;

    public InstallPackageListItem(CatalogPackage package)
        : base(new NoOpCommand())
    {
        _package = package;

        PackageVersionInfo version = _package.DefaultInstallVersion;
        var versionTagText = "Unknown";
        if (version != null)
        {
            versionTagText = version.Version == "Unknown" && version.PackageCatalog.Info.Id == "StoreEdgeFD" ? "msstore" : version.Version;
        }

        Title = _package.Name;
        Subtitle = _package.Id;
        Tags = [new Tag() { Text = versionTagText }];

        _details = new Lazy<Details?>(() => BuildDetails(version));

        _ = Task.Run(UpdatedInstalledStatus);
    }

    private Details? BuildDetails(PackageVersionInfo? version)
    {
        CatalogPackageMetadata? metadata = version?.GetCatalogPackageMetadata();
        if (metadata != null)
        {
            var description = string.IsNullOrEmpty(metadata.Description) ? metadata.ShortDescription : metadata.Description;
            var detailsBody = $"""

{description}
""";
            IconInfo heroIcon = new(string.Empty);
            IReadOnlyList<Icon> icons = metadata.Icons;
            if (icons.Count > 0)
            {
                // There's also a .Theme property we could probably use to
                // switch between default or individual icons.
                heroIcon = new IconInfo(icons[0].Url);
            }

            return new Details()
            {
                Body = detailsBody,
                Title = metadata.PackageName,
                HeroImage = heroIcon,
                Metadata = GetDetailsMetadata(metadata).ToArray(),
            };
        }

        return null;
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
        Documentation[] docs = metadata.Documentations.ToArray();
        foreach (Documentation? item in docs)
        {
            simpleData.Add(item.DocumentLabel, (string.Empty, item.DocumentUrl));
        }

        UriCreationOptions options = default;
        foreach (KeyValuePair<string, (string, string)> kv in simpleData)
        {
            var text = string.IsNullOrEmpty(kv.Value.Item1) ? kv.Value.Item2 : kv.Value.Item1;
            var target = kv.Value.Item2;
            if (!string.IsNullOrEmpty(text))
            {
                Uri? uri = null;
                Uri.TryCreate(target, options, out uri);

                DetailsElement pair = new()
                {
                    Key = kv.Key,
                    Data = new DetailsLink() { Link = uri, Text = text },
                };
                detailsElements.Add(pair);
            }
        }

        if (metadata.Tags.Any())
        {
            DetailsElement pair = new()
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
        CheckInstalledStatusResult status = await _package.CheckInstalledStatusAsync();
        bool isInstalled = _package.InstalledVersion != null;

        // might be an uninstall command
        InstallPackageCommand installCommand = new(_package, isInstalled);

        if (isInstalled)
        {
            this.Icon = InstallPackageCommand.CompletedIcon;
            this.Command = new NoOpCommand();
            List<IContextItem> contextMenu = [];
            CommandContextItem uninstallContextItem = new(installCommand)
            {
                IsCritical = true,
                Icon = InstallPackageCommand.DeleteIcon,
            };

            if (WinGetStatics.AppSearchCallback != null)
            {
                Func<string, ICommandItem?> callback = WinGetStatics.AppSearchCallback;
                ICommandItem? installedApp = callback(_package.DefaultInstallVersion == null ? _package.Name : _package.DefaultInstallVersion.DisplayName);
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
            PackageCatalogReference[] refs = WinGetStatics.AvailableCatalogs.ToArray();

            foreach (PackageCatalogReference? catalog in refs)
            {
                global::Windows.Foundation.IAsyncOperationWithProgress<RefreshPackageCatalogResult, double> operation = catalog.RefreshPackageCatalogAsync();
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
