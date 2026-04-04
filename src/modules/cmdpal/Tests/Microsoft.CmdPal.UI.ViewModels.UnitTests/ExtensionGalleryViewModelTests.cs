// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExtensionGalleryViewModelTests
{
    private static readonly string[] FeaturedOrderIds = ["beta", "alpha", "gamma"];
    private static readonly string[] NameOrderIds = ["alpha", "beta", "gamma"];
    private static readonly string[] AuthorOrderIds = ["second", "third", "first"];
    private static readonly string[] InstallationStatusOrderIds = ["update", "installed", "not-installed"];

    [TestMethod]
    public async Task LoadAsync_DoesNotBlockOnSlowSynchronousInstalledStatusKickoff()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions =
                [
                    new GalleryExtensionEntry
                    {
                        Id = "feed-extension",
                        Title = "Feed Extension",
                        Description = "Curated feed entry",
                        Author = new GalleryAuthor { Name = "Feed Author" },
                        InstallSources = [],
                    },
                ],
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .Returns(() =>
            {
                Thread.Sleep(1500);
                return Task.FromResult<IEnumerable<IExtensionWrapper>>(Array.Empty<IExtensionWrapper>());
            });

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        var loadTask = viewModel.LoadAsync();
        var completedTask = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.AreSame(loadTask, completedTask);
        Assert.IsTrue(loadTask.IsCompletedSuccessfully);
        Assert.AreEqual(1, viewModel.FilteredEntries.Count);
        Assert.AreEqual("Feed Extension", viewModel.FilteredEntries[0].Title);
    }

    [TestMethod]
    public async Task RefreshCommand_RefreshesInstalledAndWinGetStateInBackground()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions =
                [
                    new GalleryExtensionEntry
                    {
                        Id = "feed-extension",
                        Title = "Feed Extension",
                        Description = "Curated feed entry",
                        Author = new GalleryAuthor { Name = "Feed Author" },
                        InstallSources =
                        [
                            new GalleryInstallSource { Type = "winget", Id = "Contoso.FeedExtension" },
                        ],
                    },
                ],
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.RefreshInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        var winGetService = new Mock<IWinGetPackageManagerService>();
        winGetService.Setup(s => s.State).Returns(new WinGetServiceState(true, null));
        winGetService
            .Setup(s => s.RefreshCatalogsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var winGetStatusService = new Mock<IWinGetPackageStatusService>();
        winGetStatusService
            .Setup(s => s.TryGetPackageInfosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, WinGetPackageInfo>(StringComparer.OrdinalIgnoreCase));

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory(winGetService.Object, winGetStatusService.Object),
            winGetService.Object,
            winGetStatusService.Object,
            winGetOperationTrackerService: null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(1, viewModel.FilteredEntries.Count);
        Assert.AreEqual("Feed Extension", viewModel.FilteredEntries[0].Title);

        await WaitForConditionAsync(() =>
        {
            try
            {
                extensionService.Verify(s => s.RefreshInstalledExtensionsAsync(true), Times.Once);
                winGetService.Verify(s => s.RefreshCatalogsAsync(It.IsAny<CancellationToken>()), Times.Once);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    [TestMethod]
    public async Task RefreshCommand_DoesNotBlockOnSlowSynchronousStatusRefreshKickoff()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions =
                [
                    new GalleryExtensionEntry
                    {
                        Id = "feed-extension",
                        Title = "Feed Extension",
                        Description = "Curated feed entry",
                        Author = new GalleryAuthor { Name = "Feed Author" },
                        InstallSources =
                        [
                            new GalleryInstallSource { Type = "winget", Id = "Contoso.FeedExtension" },
                        ],
                    },
                ],
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.RefreshInstalledExtensionsAsync(true))
            .Returns(() =>
            {
                Thread.Sleep(1500);
                return Task.FromResult<IEnumerable<IExtensionWrapper>>(Array.Empty<IExtensionWrapper>());
            });

        var winGetService = new Mock<IWinGetPackageManagerService>();
        winGetService.Setup(s => s.State).Returns(new WinGetServiceState(true, null));
        winGetService
            .Setup(s => s.RefreshCatalogsAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Thread.Sleep(1500);
                return Task.FromResult(true);
            });

        var winGetStatusService = new Mock<IWinGetPackageStatusService>();
        winGetStatusService
            .Setup(s => s.TryGetPackageInfosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Thread.Sleep(1500);
                return Task.FromResult<IReadOnlyDictionary<string, WinGetPackageInfo>?>(
                    new Dictionary<string, WinGetPackageInfo>(StringComparer.OrdinalIgnoreCase));
            });

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory(winGetService.Object, winGetStatusService.Object),
            winGetService.Object,
            winGetStatusService.Object,
            winGetOperationTrackerService: null);

        var refreshTask = viewModel.RefreshCommand.ExecuteAsync(null);
        var completedTask = await Task.WhenAny(refreshTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.AreSame(refreshTask, completedTask);
        Assert.IsTrue(refreshTask.IsCompletedSuccessfully);
        Assert.AreEqual(1, viewModel.FilteredEntries.Count);
        Assert.AreEqual("Feed Extension", viewModel.FilteredEntries[0].Title);
    }

    [TestMethod]
    public async Task LoadAsync_DoesNotShowFallbackCacheWarning_ForNormalCacheHits()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions =
                [
                    new GalleryExtensionEntry
                    {
                        Id = "cached-extension",
                        Title = "Cached Extension",
                        Description = "Cached feed entry",
                        Author = new GalleryAuthor { Name = "Feed Author" },
                        InstallSources = [],
                    },
                ],
                FromCache = true,
                UsedFallbackCache = false,
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.FromCache);
        Assert.IsFalse(viewModel.UsedFallbackCache);
    }

    [TestMethod]
    public async Task LoadAsync_ShowsFallbackCacheWarning_WhenServiceFallsBackToCache()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions =
                [
                    new GalleryExtensionEntry
                    {
                        Id = "cached-extension",
                        Title = "Cached Extension",
                        Description = "Cached feed entry",
                        Author = new GalleryAuthor { Name = "Feed Author" },
                        InstallSources = [],
                    },
                ],
                FromCache = true,
                UsedFallbackCache = true,
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.FromCache);
        Assert.IsTrue(viewModel.UsedFallbackCache);
    }

    [TestMethod]
    public async Task LoadAsync_ShowsErrorSurface_WhenGalleryIsRateLimited()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                HasError = true,
                IsRateLimited = true,
                ErrorMessage = null,
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasError);
        Assert.IsTrue(viewModel.IsRateLimitedError);
        Assert.IsTrue(viewModel.ShowErrorSurface);
        Assert.IsFalse(viewModel.ShowErrorInfoBar);
        Assert.AreEqual("The gallery is taking a breather", viewModel.ErrorDisplayTitle);
        Assert.AreEqual(0, viewModel.FilteredEntries.Count);
    }

    [TestMethod]
    public async Task LoadAsync_ShowsGenericErrorSurface_WhenGalleryLoadFailsWithoutEntries()
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                HasError = true,
                ErrorMessage = "Service unavailable",
            });

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasError);
        Assert.IsFalse(viewModel.IsRateLimitedError);
        Assert.IsTrue(viewModel.ShowErrorSurface);
        Assert.IsFalse(viewModel.ShowErrorInfoBar);
        Assert.AreEqual("Failed to load extensions", viewModel.ErrorDisplayTitle);
        Assert.AreEqual("Service unavailable", viewModel.ErrorDisplayMessage);
        Assert.AreEqual(0, viewModel.FilteredEntries.Count);
    }

    [TestMethod]
    public async Task SortByNameCommand_SortsEntriesByTitle()
    {
        var galleryService = CreateGalleryService(
            CreateGalleryEntry("beta", "Beta Extension", "Contoso B"),
            CreateGalleryEntry("alpha", "Alpha Extension", "Contoso A"),
            CreateGalleryEntry("gamma", "Gamma Extension", "Contoso C"));

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        CollectionAssert.AreEqual(
            FeaturedOrderIds,
            viewModel.FilteredEntries.Select(entry => entry.Id).ToArray());

        viewModel.SortByNameCommand.Execute(null);

        CollectionAssert.AreEqual(
            NameOrderIds,
            viewModel.FilteredEntries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public async Task SortByAuthorCommand_SortsEntriesByAuthor()
    {
        var galleryService = CreateGalleryService(
            CreateGalleryEntry("first", "First Extension", "Charlie"),
            CreateGalleryEntry("second", "Second Extension", "Alice"),
            CreateGalleryEntry("third", "Third Extension", "Bob"));

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(Array.Empty<IExtensionWrapper>());

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory());

        await viewModel.LoadAsync();

        viewModel.SortByAuthorCommand.Execute(null);

        CollectionAssert.AreEqual(
            AuthorOrderIds,
            viewModel.FilteredEntries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public async Task SortByInstallationStatusCommand_SortsUpdatesBeforeInstalledBeforeNotInstalled()
    {
        var galleryService = CreateGalleryService(
            CreateGalleryEntry(
                "not-installed",
                "Not Installed Extension",
                "Author A",
                winGetId: "Contoso.NotInstalled"),
            CreateGalleryEntry(
                "installed",
                "Installed Extension",
                "Author B",
                packageFamilyName: "Contoso.Installed_12345"),
            CreateGalleryEntry(
                "update",
                "Update Extension",
                "Author C",
                winGetId: "Contoso.Update"));

        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(s => s.GetInstalledExtensionsAsync(true))
            .ReturnsAsync(
            [
                CreateInstalledExtensionWrapper("Contoso.Installed_12345"),
            ]);

        var winGetStatusService = new Mock<IWinGetPackageStatusService>();
        winGetStatusService
            .Setup(s => s.TryGetPackageInfosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, WinGetPackageInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["Contoso.NotInstalled"] = new(
                    new WinGetPackageStatus(
                        IsInstalled: false,
                        IsInstalledStateKnown: true,
                        IsUpdateAvailable: false,
                        IsUpdateStateKnown: true),
                    Details: null),
                ["Contoso.Update"] = new(
                    new WinGetPackageStatus(
                        IsInstalled: true,
                        IsInstalledStateKnown: true,
                        IsUpdateAvailable: true,
                        IsUpdateStateKnown: true),
                    Details: null),
            });

        using var viewModel = new ExtensionGalleryViewModel(
            galleryService.Object,
            extensionService.Object,
            NullLogger<ExtensionGalleryViewModel>.Instance,
            CreateGalleryExtensionViewModelFactory(winGetPackageStatusService: winGetStatusService.Object),
            winGetPackageManagerService: null,
            winGetStatusService.Object,
            winGetOperationTrackerService: null);

        await viewModel.LoadAsync();
        await WaitForConditionAsync(() =>
            viewModel.FilteredEntries.Count == 3
            && viewModel.FilteredEntries.Any(entry => entry.Id == "update" && entry.IsUpdateAvailable)
            && viewModel.FilteredEntries.Any(entry => entry.Id == "not-installed" && entry.IsInstalledStateKnown));

        viewModel.SortByInstallationStatusCommand.Execute(null);

        CollectionAssert.AreEqual(
            InstallationStatusOrderIds,
            viewModel.FilteredEntries.Select(entry => entry.Id).ToArray());
    }

    private static Mock<IExtensionGalleryService> CreateGalleryService(params GalleryExtensionEntry[] entries)
    {
        var galleryService = new Mock<IExtensionGalleryService>();
        galleryService.Setup(s => s.IsCustomFeed).Returns(false);
        galleryService.Setup(s => s.GetBaseUrl()).Returns("https://example.com/index.json");
        galleryService
            .Setup(s => s.FetchExtensionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryFetchResult
            {
                Extensions = [.. entries],
            });

        return galleryService;
    }

    private static GalleryExtensionEntry CreateGalleryEntry(
        string id,
        string title,
        string authorName,
        string? winGetId = null,
        string? packageFamilyName = null)
    {
        List<GalleryInstallSource> installSources = [];
        if (!string.IsNullOrWhiteSpace(winGetId))
        {
            installSources.Add(new GalleryInstallSource
            {
                Type = "winget",
                Id = winGetId,
            });
        }

        return new GalleryExtensionEntry
        {
            Id = id,
            Title = title,
            Description = $"{title} description",
            Author = new GalleryAuthor { Name = authorName },
            Detection = string.IsNullOrWhiteSpace(packageFamilyName)
                ? null
                : new GalleryDetection { PackageFamilyName = packageFamilyName },
            InstallSources = installSources,
        };
    }

    private static IExtensionWrapper CreateInstalledExtensionWrapper(string packageFamilyName)
    {
        var wrapper = new Mock<IExtensionWrapper>();
        wrapper.SetupGet(w => w.PackageFamilyName).Returns(packageFamilyName);
        return wrapper.Object;
    }

    private static ExtensionGalleryItemViewModelFactory CreateGalleryExtensionViewModelFactory(
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null)
    {
        return new ExtensionGalleryItemViewModelFactory(
            NullLogger<ExtensionGalleryItemViewModel>.Instance,
            winGetPackageManagerService,
            winGetPackageStatusService,
            winGetOperationTrackerService);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMilliseconds = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start >= timeoutMilliseconds)
            {
                Assert.Fail("Timed out waiting for the expected condition.");
            }

            await Task.Delay(25);
        }
    }
}
