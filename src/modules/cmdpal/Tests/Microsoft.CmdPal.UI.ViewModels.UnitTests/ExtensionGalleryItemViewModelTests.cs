// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExtensionGalleryItemViewModelTests
{
    private static readonly Uri ExpectedIconPlaceholderUri = new Uri("ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png");

    [TestMethod]
    public void Constructor_UsesPlaceholderIcon_WhenIconIsMissing()
    {
        var entry = CreateEntry(iconUrl: null);
        var viewModel = CreateViewModel(entry);

        Assert.AreEqual(ExpectedIconPlaceholderUri, viewModel.IconUri);
    }

    [TestMethod]
    public void Constructor_UsesPlaceholderIcon_WhenIconUriIsInvalid()
    {
        var entry = CreateEntry(iconUrl: "iconUrl.png");
        var viewModel = CreateViewModel(entry);

        Assert.AreEqual(ExpectedIconPlaceholderUri, viewModel.IconUri);
    }

    [TestMethod]
    public void Constructor_UsesAbsoluteIconUri_WhenIconUriIsValid()
    {
        var expected = new Uri("https://example.com/iconUrl.png");
        var entry = CreateEntry(iconUrl: expected.AbsoluteUri);
        var viewModel = CreateViewModel(entry);

        Assert.AreEqual(expected, viewModel.IconUri);
    }

    [TestMethod]
    public void Constructor_UsesFallbackDisplayValues_WhenLocalizedMetadataMissing()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "fallback-extension",
            Title = string.Empty,
            Description = string.Empty,
            Author = new GalleryAuthor { Name = string.Empty },
            InstallSources = new List<GalleryInstallSource>(),
        };

        var viewModel = CreateViewModel(entry);

        Assert.AreEqual("fallback-extension", viewModel.DisplayTitle);
        Assert.AreEqual("No description available.", viewModel.DisplayDescription);
        Assert.AreEqual("Unknown author", viewModel.DisplayAuthorName);
        Assert.IsTrue(viewModel.ShowUnknownSourceIndicator);
    }

    [TestMethod]
    public void Constructor_NormalizesAndCollectsSources_Generically()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "source-extension",
            Title = "Source extension",
            Description = "Source extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
                new GalleryInstallSource { Type = "msstore", Id = "9TEST1234ABC" },
                new GalleryInstallSource { Type = "url", Uri = "https://github.com/contoso/extension" },
                new GalleryInstallSource { Type = "customSourceType", Uri = "https://example.com/custom" },
            ],
        };

        var viewModel = CreateViewModel(entry);

        Assert.IsTrue(viewModel.HasWinGetSource);
        Assert.IsTrue(viewModel.HasStoreSource);
        Assert.IsTrue(viewModel.HasGitHubSource);
        Assert.IsTrue(viewModel.HasUnknownSource);
        Assert.IsTrue(viewModel.Sources.Count >= 4);
    }

    [TestMethod]
    public void Constructor_EnablesCopyCommand_WhenWinGetIdIsAvailable()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "copy-command-extension",
            Title = "Copy command extension",
            Description = "Copy command extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var viewModel = CreateViewModel(entry);

        Assert.IsTrue(viewModel.CanCopyWinGetInstallCommand);
        Assert.AreEqual("winget install --id Contoso.Extension", viewModel.WinGetInstallCommand);
    }

    [TestMethod]
    public void Constructor_DisablesCopyCommand_WhenWinGetIdIsMissing()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "copy-command-no-id-extension",
            Title = "Copy command no id extension",
            Description = "Copy command no id extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = string.Empty },
            ],
        };

        var viewModel = CreateViewModel(entry);

        Assert.IsFalse(viewModel.CanCopyWinGetInstallCommand);
        Assert.AreEqual(string.Empty, viewModel.WinGetInstallCommand);
    }

    [TestMethod]
    public void Constructor_ExposesManifestTags_WhenProvided()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "tagged-extension",
            Title = "Tagged extension",
            Description = "Tagged extension description",
            Author = new GalleryAuthor { Name = "Author" },
            Tags =
            [
                "developer tools",
                "productivity",
            ],
            InstallSources = [],
        };

        var viewModel = CreateViewModel(entry);

        Assert.IsTrue(viewModel.HasTags);
        Assert.AreEqual("developer tools, productivity", viewModel.TagsText);
    }

    [TestMethod]
    public void ApplyWinGetPackageInfo_UpdatesStatus_WhenDetailsAreMissing()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "winget-status-extension",
            Title = "WinGet status extension",
            Description = "WinGet status extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var viewModel = CreateViewModel(entry);
        viewModel.ApplyWinGetPackageInfo(
            new WinGetPackageInfo(
                new WinGetPackageStatus(
                    IsInstalled: true,
                    IsInstalledStateKnown: true,
                    IsUpdateAvailable: false,
                    IsUpdateStateKnown: true),
                Details: null));

        Assert.IsTrue(viewModel.IsInstalled);
        Assert.IsTrue(viewModel.IsInstalledStateKnown);
        Assert.IsFalse(viewModel.IsUpdateAvailable);
        Assert.IsTrue(viewModel.IsUpdateStateKnown);
        Assert.IsFalse(viewModel.HasSourceMetadataDetails);
    }

    [TestMethod]
    public void ShowWinGetStatusDetails_IsFalse_WhenWinGetStatusDuplicatesInstallStatus()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "winget-status-extension",
            Title = "WinGet status extension",
            Description = "WinGet status extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var viewModel = CreateViewModel(entry);
        viewModel.ApplyWinGetPackageInfo(
            new WinGetPackageInfo(
                new WinGetPackageStatus(
                    IsInstalled: false,
                    IsInstalledStateKnown: true,
                    IsUpdateAvailable: false,
                    IsUpdateStateKnown: true),
                Details: null));

        Assert.AreEqual("Not installed", viewModel.InstallStatusText);
        Assert.AreEqual("Not installed.", viewModel.WinGetStatusText);
        Assert.IsFalse(viewModel.ShowWinGetStatusDetails);
    }

    [TestMethod]
    public void ApplyWinGetPackageInfo_AttachesSourceDetails_WhenMetadataIsAvailable()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "winget-details-extension",
            Title = "WinGet details extension",
            Description = "WinGet details extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var details = new WinGetPackageDetails(
            Name: "Contoso Extension",
            Version: "1.2.3",
            Summary: "Summary",
            Description: "Description",
            Publisher: "Contoso",
            PublisherUrl: "https://contoso.example/publisher",
            PublisherSupportUrl: "https://contoso.example/support",
            Author: "Contoso Team",
            License: "MIT",
            LicenseUrl: "https://contoso.example/license",
            PackageUrl: "https://contoso.example/package",
            ReleaseNotes: "Release notes",
            ReleaseNotesUrl: "https://contoso.example/release-notes",
            IconUrl: "https://contoso.example/iconUrl.png",
            DocumentationLinks:
            [
                new WinGetNamedLink("Docs", "https://contoso.example/docs"),
            ],
            Tags:
            [
                "utility",
                "productivity",
            ]);

        var viewModel = CreateViewModel(entry);
        viewModel.ApplyWinGetPackageInfo(
            new WinGetPackageInfo(
                new WinGetPackageStatus(
                    IsInstalled: true,
                    IsInstalledStateKnown: true,
                    IsUpdateAvailable: true,
                    IsUpdateStateKnown: true),
                details));

        Assert.IsTrue(viewModel.HasSourceMetadataDetails);
        GallerySourceInfo? wingetSource = null;
        for (var i = 0; i < viewModel.Sources.Count; i++)
        {
            if (string.Equals(viewModel.Sources[i].Kind, "winget", StringComparison.OrdinalIgnoreCase))
            {
                wingetSource = viewModel.Sources[i];
                break;
            }
        }

        Assert.IsNotNull(wingetSource);
        var sourceDetails = wingetSource!.Details;
        Assert.IsNotNull(sourceDetails);
        Assert.AreEqual("Summary", sourceDetails.Summary);
        Assert.AreEqual("1.2.3", sourceDetails.Version);
        Assert.IsTrue(sourceDetails.Items.Count > 0);
        Assert.IsTrue(sourceDetails.Tags.Count > 0);
    }

    [TestMethod]
    public void ApplyTrackedOperation_ShowsTileProgress_WhenDownloading()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "winget-progress-extension",
            Title = "WinGet progress extension",
            Description = "WinGet progress extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var viewModel = CreateViewModel(entry);
        viewModel.ApplyTrackedOperation(new WinGetPackageOperation(
            OperationId: Guid.NewGuid(),
            PackageId: "Contoso.Extension",
            PackageName: "Contoso Extension",
            Kind: WinGetPackageOperationKind.Install,
            State: WinGetPackageOperationState.Downloading,
            CanCancel: true,
            IsIndeterminate: false,
            ProgressPercent: 42,
            BytesDownloaded: 420,
            BytesRequired: 1000,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CompletedAt: null));

        Assert.IsTrue(viewModel.IsWinGetActionInProgress);
        Assert.IsTrue(viewModel.ShowWinGetActionIndicator);
        Assert.IsTrue(viewModel.ShowWinGetActionStatus);
        Assert.IsTrue(viewModel.CanCancelWinGetAction);
        Assert.IsTrue(viewModel.ShowCancelWinGetActionButton);
        Assert.IsFalse(viewModel.IsWinGetActionIndeterminate);
        Assert.AreEqual(42d, viewModel.WinGetActionProgressValue);
        StringAssert.Contains(viewModel.WinGetActionMessage!, "42%");
    }

    [TestMethod]
    public void CancelWinGetActionCommand_RequestsCancellation_FromTracker()
    {
        var entry = new GalleryExtensionEntry
        {
            Id = "winget-cancel-extension",
            Title = "WinGet cancel extension",
            Description = "WinGet cancel extension description",
            Author = new GalleryAuthor { Name = "Author" },
            InstallSources =
            [
                new GalleryInstallSource { Type = "winget", Id = "Contoso.Extension" },
            ],
        };

        var operation = new WinGetPackageOperation(
            OperationId: Guid.NewGuid(),
            PackageId: "Contoso.Extension",
            PackageName: "Contoso Extension",
            Kind: WinGetPackageOperationKind.Install,
            State: WinGetPackageOperationState.Downloading,
            CanCancel: true,
            IsIndeterminate: false,
            ProgressPercent: 42,
            BytesDownloaded: 420,
            BytesRequired: 1000,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CompletedAt: null);

        var tracker = new Mock<IWinGetOperationTrackerService>();
        tracker.Setup(t => t.GetLatestOperation("Contoso.Extension")).Returns(operation);
        tracker.Setup(t => t.TryCancelOperation(operation.OperationId)).Returns(true);

        var viewModel = CreateViewModel(entry, winGetOperationTrackerService: tracker.Object);

        viewModel.ApplyTrackedOperation(operation);
        Assert.IsTrue(viewModel.CancelWinGetActionCommand.CanExecute(null));

        viewModel.CancelWinGetActionCommand.Execute(null);

        tracker.Verify(t => t.TryCancelOperation(operation.OperationId), Times.Once);
    }

    private static GalleryExtensionEntry CreateEntry(string? iconUrl)
    {
        return new GalleryExtensionEntry
        {
            Id = "sample-extension",
            Title = "Sample",
            Description = "Sample extension",
            Author = new GalleryAuthor { Name = "Sample Author" },
            IconUrl = iconUrl,
            InstallSources = new List<GalleryInstallSource>(),
        };
    }

    private static ExtensionGalleryItemViewModel CreateViewModel(
        GalleryExtensionEntry entry,
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null)
    {
        return new ExtensionGalleryItemViewModel(
            entry,
            NullLogger<ExtensionGalleryItemViewModel>.Instance,
            winGetPackageManagerService,
            winGetPackageStatusService,
            winGetOperationTrackerService);
    }
}
