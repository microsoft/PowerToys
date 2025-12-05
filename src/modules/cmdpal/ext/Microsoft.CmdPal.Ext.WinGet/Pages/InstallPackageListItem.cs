// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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

        PackageVersionInfo? version = null;
        try
        {
            version = _package.DefaultInstallVersion ?? _package.InstalledVersion;
        }
        catch (Exception e)
        {
            Logger.LogError("Could not get package version", e);
        }

        var versionTagText = "Unknown";
        if (version is not null)
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
        CatalogPackageMetadata? metadata = null;
        try
        {
            metadata = version?.GetCatalogPackageMetadata();
        }
        catch (COMException ex)
        {
            Logger.LogWarning($"GetCatalogPackageMetadata error {ex.ErrorCode}");
        }

        if (metadata is not null)
        {
            for (var i = 0; i < metadata.Tags.Count; i++)
            {
                if (metadata.Tags[i].Equals(WinGetExtensionPage.ExtensionsTag, StringComparison.OrdinalIgnoreCase))
                {
                    if (_installCommand is not null)
                    {
                        _installCommand.SkipDependencies = true;
                    }

                    break;
                }
            }

            var description = string.IsNullOrEmpty(metadata.Description) ?
                metadata.ShortDescription :
                metadata.Description;
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
            { Properties.Resources.winget_publisher_support, (string.Empty, metadata.PublisherSupportUrl) },

            // The link to the release notes will only show up if there is an
            // actual URL for the release notes
            { Properties.Resources.winget_view_release_notes, (string.IsNullOrEmpty(metadata.ReleaseNotesUrl) ? string.Empty : Properties.Resources.winget_view_online, metadata.ReleaseNotesUrl) },

            // These can be l o n g
            { Properties.Resources.winget_release_notes, (metadata.ReleaseNotes, string.Empty) },
        };
        var docs = metadata.Documentations;
        var count = docs.Count;
        for (var i = 0; i < count; i++)
        {
            var item = docs[i];
            simpleData.Add(item.DocumentLabel, (string.Empty, item.DocumentUrl));
        }

        UriCreationOptions options = default;
        foreach (var kv in simpleData)
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

        if (metadata.Tags.Count > 0)
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
        try
        {
            var status = await _package.CheckInstalledStatusAsync();
        }
        catch (OperationCanceledException)
        {
            // DO NOTHING HERE
            return;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            ExtensionHost.LogMessage($"[WinGet] UpdatedInstalledStatus throw exception: {ex.Message}");
            Logger.LogError($"[WinGet] UpdatedInstalledStatus throw exception", ex);
            return;
        }

        var isInstalled = _package.InstalledVersion is not null;

        var installedState = isInstalled ?
            (_package.IsUpdateAvailable ? PackageInstallCommandState.Update : PackageInstallCommandState.Uninstall) :
            PackageInstallCommandState.Install;

        // might be an uninstall command
        InstallPackageCommand installCommand = new(_package, installedState);

        if (_package.InstalledVersion is not null)
        {
#if DEBUG
            var installerType = _package.InstalledVersion.GetMetadata(PackageVersionMetadataField.InstallerType);
            Subtitle = installerType + " | " + Subtitle;
#endif

            List<IContextItem> contextMenu = [];
            Command = installCommand;
            Icon = installedState switch
            {
                PackageInstallCommandState.Install => Icons.DownloadIcon,
                PackageInstallCommandState.Update => Icons.UpdateIcon,
                PackageInstallCommandState.Uninstall => Icons.CompletedIcon,
                _ => Icons.DownloadIcon,
            };

            TryLocateAndAppendActionForApp(contextMenu);

            MoreCommands = contextMenu.ToArray();
        }
        else
        {
            _installCommand = new InstallPackageCommand(_package, installedState);
            _installCommand.InstallStateChanged += InstallStateChangedHandler;
            Command = _installCommand;
            Icon = _installCommand.Icon;
        }
    }

    private void TryLocateAndAppendActionForApp(List<IContextItem> contextMenu)
    {
        try
        {
            // Let's try to connect it to an installed app if possible
            // This is a bit of dark magic, since there's no direct link between
            // WinGet packages and installed apps.
            var lookupByPackageName = WinGetStatics.AppSearchByPackageFamilyNameCallback;
            if (lookupByPackageName is not null)
            {
                var names = _package.InstalledVersion.PackageFamilyNames;
                for (var i = 0; i < names.Count; i++)
                {
                    var installedAppByPfn = lookupByPackageName(names[i]);
                    if (installedAppByPfn is not null)
                    {
                        contextMenu.Add(new Separator());
                        contextMenu.Add(new CommandContextItem(installedAppByPfn.Command));
                        foreach (var item in installedAppByPfn.MoreCommands)
                        {
                            contextMenu.Add(item);
                        }

                        return;
                    }
                }
            }

            var lookupByProductCode = WinGetStatics.AppSearchByProductCodeCallback;
            if (lookupByProductCode is not null)
            {
                var productCodes = _package.InstalledVersion.ProductCodes;
                for (var i = 0; i < productCodes.Count; i++)
                {
                    var installedAppByProductCode = lookupByProductCode(productCodes[i]);
                    if (installedAppByProductCode is not null)
                    {
                        contextMenu.Add(new Separator());
                        contextMenu.Add(new CommandContextItem(installedAppByProductCode.Command));
                        foreach (var item in installedAppByProductCode.MoreCommands)
                        {
                            contextMenu.Add(item);
                        }

                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to retrieve app context menu items for package '{_package?.Name ?? "Unknown"}'", ex);
        }
    }

    private void InstallStateChangedHandler(object? sender, InstallPackageCommand e)
    {
        if (!ApiInformation.IsApiContractPresent("Microsoft.Management.Deployment.WindowsPackageManagerContract", 12))
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
            var refs = WinGetStatics.AvailableCatalogs;
            for (var i = 0; i < refs.Count; i++)
            {
                var catalog = refs[i];
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
