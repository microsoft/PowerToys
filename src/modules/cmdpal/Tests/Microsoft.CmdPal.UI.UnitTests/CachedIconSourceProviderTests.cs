// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public partial class CachedIconSourceProviderTests
{
    [TestMethod]
    [Timeout(5_000)]
    public async Task ConcurrentRequestsShareOneInFlightLoad()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "test" };
        var requests = new ConcurrentBag<Task<IconSource?>>();

        Parallel.For(0, 32, _ => requests.Add(provider.GetIconSource(icon, 1.0)));

        var requestArray = requests.ToArray();
        Assert.HasCount(32, requestArray);
        Assert.AreEqual(1, loader.EnqueueCount);
        foreach (var request in requestArray)
        {
            Assert.AreSame(requestArray[0], request);
        }

        loader.CompleteNext(null);
        await Task.WhenAll(requestArray);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SuccessfulLoadIsCachedBeforeInFlightEntryIsRemoved()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "test" };

        var first = provider.GetIconSource(icon, 1.0);
        loader.CompleteNext(null);
        await first;

        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)),
            "The completed load was not retired from the in-flight dictionary.");

        var cached = provider.GetIconSource(icon, 1.0);

        Assert.AreSame(first, cached);
        Assert.AreEqual(1, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SharedInFlightLoadTracksEveryLiveRequest()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "test" };
        var firstDemand = new IconRequestDemand();
        var secondDemand = new IconRequestDemand();

        var first = provider.GetIconSource(icon, 1.0, demand: firstDemand);
        var second = provider.GetIconSource(icon, 1.0, demand: secondDemand);

        Assert.AreSame(first, second);
        Assert.IsNotNull(loader.LastDemand);
        Assert.IsTrue(loader.LastDemand.IsDemanded);

        firstDemand.Release();
        Assert.IsTrue(loader.LastDemand.IsDemanded);

        secondDemand.Release();
        Assert.IsFalse(loader.LastDemand.IsDemanded);

        var returnedDemand = new IconRequestDemand();
        var returned = provider.GetIconSource(icon, 1.0, demand: returnedDemand);
        Assert.AreSame(first, returned);
        Assert.IsTrue(loader.LastDemand.IsDemanded);

        returnedDemand.Release();
        Assert.IsFalse(loader.LastDemand.IsDemanded);

        loader.CompleteNext(null);
        await Task.WhenAll(first, second, returned);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SuccessfulDirectGlyphLoadIsCachedWithoutQueueing()
    {
        var glyph = CreateTestIconSource();
        var loader = new ControllableIconLoader
        {
            ReturnDirectGlyph = true,
            DirectGlyphResult = glyph,
        };
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "\uE700" };

        var first = provider.GetIconSource(icon, 1.0);
        var firstResult = await first;

        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)),
            "The direct glyph load was not retired from the in-flight dictionary.");

        var cached = provider.GetIconSource(icon, 1.0);

        Assert.AreSame(glyph, firstResult);
        Assert.AreSame(first, cached);
        Assert.AreEqual(1, loader.GlyphAttemptCount);
        Assert.AreEqual(0, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CachedProviderFallsBackWhenLoaderViolatesDirectGlyphContract()
    {
        var loader = new ControllableIconLoader { ReturnDirectGlyph = true };
        var provider = CreateProvider(loader);

        var result = provider.GetIconSource(new IconDataViewModel { Icon = "glyph" }, 1.0);

        Assert.AreEqual(1, loader.GlyphAttemptCount);
        Assert.AreEqual(1, loader.EnqueueCount);
        loader.CompleteNext(null);
        Assert.IsNull(await result);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task GlyphAndOtherEntriesUseIndependentCacheCapacities()
    {
        var glyphResult = CreateTestIconSource();
        var loader = new ControllableIconLoader
        {
            ReturnDirectGlyph = true,
            DirectGlyphResult = glyphResult,
        };
        var provider = CreateProvider(loader, glyphCacheSize: 1, otherCacheSize: 1);
        var glyph = new IconDataViewModel { Icon = "\uE700" };
        var other = new IconDataViewModel { Icon = "bitmap.png" };

        var glyphLoad = provider.GetIconSource(glyph, 1.0);
        await glyphLoad;

        loader.ReturnDirectGlyph = false;
        var otherLoad = provider.GetIconSource(other, 1.0);
        loader.CompleteNext(null);
        await otherLoad;

        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => GetInFlightCount(provider) == 0 &&
                    GetCacheCount(provider, "_glyphCache") == 1 &&
                    GetCacheCount(provider, "_otherCache") == 1,
                TimeSpan.FromSeconds(2)),
            "The completed entries were not added to their independent caches.");

        Assert.AreSame(glyphLoad, provider.GetIconSource(glyph, 1.0));
        Assert.AreSame(otherLoad, provider.GetIconSource(other, 1.0));
        Assert.AreEqual(1, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DistinctStreamReferencesWithCollidingRuntimeHashesDoNotShareLoad()
    {
        var collision = FindRuntimeHashCollision();
        if (collision is null)
        {
            Assert.Inconclusive("No runtime identity-hash collision was found among 500,000 live objects.");
            return;
        }

        var (firstStream, secondStream) = collision.Value;
        Assert.AreNotSame(firstStream, secondStream);
        Assert.AreEqual(RuntimeHelpers.GetHashCode(firstStream), RuntimeHelpers.GetHashCode(secondStream));

        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var firstIcon = new IconDataViewModel
        {
            Data = new IconDataStreamReference { Unsafe = firstStream },
        };
        var secondIcon = new IconDataViewModel
        {
            Data = new IconDataStreamReference { Unsafe = secondStream },
        };

        var first = provider.GetIconSource(firstIcon, 1.0);
        var second = provider.GetIconSource(secondIcon, 1.0);

        Assert.AreNotSame(first, second);
        Assert.AreEqual(2, loader.EnqueueCount);

        loader.CompleteNext(null);
        loader.CompleteNext(null);
        await Task.WhenAll(first, second);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CanonicallyEquivalentInitialsShareCacheEntry()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var precomposed = new IconDataViewModel { Icon = "|Initials|Å|#0067C0|circle|" };
        var decomposed = new IconDataViewModel { Icon = "|Initials|A\u030A|#0067C0|circle|" };
        var percentEncoded = new IconDataViewModel { Icon = "|Initials|%C3%85|#0067C0|circle|" };

        var first = provider.GetIconSource(precomposed, 1.0, theme: ElementTheme.Light);
        loader.CompleteNext(null);
        await first;
        Assert.IsTrue(SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));

        Assert.AreSame(first, provider.GetIconSource(decomposed, 1.0, theme: ElementTheme.Light));
        Assert.AreSame(first, provider.GetIconSource(percentEncoded, 1.0, theme: ElementTheme.Light));
        Assert.AreEqual(1, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ThemedSvgProtocolUsesDistinctCacheEntriesAcrossThemes()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel
        {
            Icon = "|ThemedSvg|<svg xmlns=\"http://www.w3.org/2000/svg\"><path fill=\"{{ThemeColor}}\"/></svg>",
        };

        var light = provider.GetIconSource(icon, 1.0, theme: ElementTheme.Light);
        loader.CompleteNext(null);
        await light;
        Assert.IsTrue(SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));

        var dark = provider.GetIconSource(icon, 1.0, theme: ElementTheme.Dark);
        Assert.AreNotSame(light, dark);
        loader.CompleteNext(null);
        await dark;
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => GetInFlightCount(provider) == 0 && GetCacheCount(provider, "_otherCache") == 2,
                TimeSpan.FromSeconds(2)));

        Assert.AreSame(light, provider.GetIconSource(icon, 1.0, theme: ElementTheme.Light));
        Assert.AreSame(dark, provider.GetIconSource(icon, 1.0, theme: ElementTheme.Dark));
        Assert.AreEqual(2, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task PlainSvgProtocolSharesCacheEntryAcrossThemes()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel
        {
            Icon = "|Svg|<svg xmlns=\"http://www.w3.org/2000/svg\"><path fill=\"#0067C0\"/></svg>",
        };

        var light = provider.GetIconSource(icon, 1.0, theme: ElementTheme.Light);
        loader.CompleteNext(null);
        await light;
        Assert.IsTrue(SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));

        Assert.AreSame(light, provider.GetIconSource(icon, 1.0, theme: ElementTheme.Dark));
        Assert.AreEqual(1, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DifferentLegacyPathsWithSameShellIdentityShareCanonicalLoad()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var firstIcon = new IconDataViewModel { Icon = "C:\\Files\\first.txt" };
        var secondIcon = new IconDataViewModel { Icon = "C:\\Files\\second.txt" };

        var first = provider.GetIconSource(firstIcon, 1.0);
        var second = provider.GetIconSource(secondIcon, 1.0);

        Assert.AreEqual(2, loader.ShellEnqueueCount);
        Assert.AreEqual(0, loader.EnqueueCount);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        Assert.AreEqual(1, loader.ShellExtractionCount);

        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);
        var results = await Task.WhenAll(first, second);
        Assert.AreSame(source, results[0]);
        Assert.AreSame(source, results[1]);
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => GetInFlightCount(provider) == 0 && GetCacheCount(provider, "_otherCache") == 1,
                TimeSpan.FromSeconds(2)));

        var repeated = provider.GetIconSource(firstIcon, 1.0);
        Assert.AreSame(first, repeated);
        Assert.AreEqual(2, loader.ShellEnqueueCount);

        var third = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\third.txt" },
            1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        Assert.AreSame(source, await third);
        Assert.AreEqual(3, loader.ShellEnqueueCount);
        Assert.AreEqual(1, loader.ShellExtractionCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ProgressiveShellRequestPublishesTypeIconBeforeSameIdentityCompletes()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest();
        var icon = new IconDataViewModel
        {
            Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\first.dll"),
        };

        var finalTask = provider.GetIconSource(icon, 1.0, demand: progress);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(42, ShellItemIconLocationMode.FileType);
        var typeSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(typeSource);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2 && progress.Intermediate is not null,
            TimeSpan.FromSeconds(2)));
        Assert.AreSame(typeSource, progress.Intermediate);

        loader.LocateNextShellItem(42, ShellItemIconLocationMode.ExactItem);
        Assert.AreSame(typeSource, await finalTask);
        Assert.AreEqual(1, loader.ShellExtractionCount);
        progress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ProgressiveShellRequestReplacesTypeIconWhenExactIdentityDiffers()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest();
        var icon = new IconDataViewModel
        {
            Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\custom.exe"),
        };

        var finalTask = provider.GetIconSource(icon, 1.0, demand: progress);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(7, ShellItemIconLocationMode.FileType);
        var typeSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(typeSource);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2 && progress.Intermediate is not null,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(99, ShellItemIconLocationMode.ExactItem);
        var exactSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(exactSource);

        Assert.AreSame(exactSource, await finalTask);
        Assert.AreSame(typeSource, progress.Intermediate);
        Assert.AreEqual(2, loader.ShellExtractionCount);
        progress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ProgressiveShellRequestKeepsTypeIconWhenExactRefinementFails()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest();
        var icon = new IconDataViewModel
        {
            Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\custom.exe"),
        };

        var finalTask = provider.GetIconSource(icon, 1.0, demand: progress);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(7, ShellItemIconLocationMode.FileType);
        var typeSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(typeSource);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2 && progress.Intermediate is not null,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(99, ShellItemIconLocationMode.ExactItem);
        loader.FailNextShellOwner(new InvalidOperationException("Exact lookup failed."));

        Assert.AreSame(typeSource, await finalTask);
        progress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ProgressiveShellRequestContinuesWhenIntermediatePresentationFails()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest { ThrowOnIntermediate = true };
        var icon = new IconDataViewModel
        {
            Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\custom.exe"),
        };

        var finalTask = provider.GetIconSource(icon, 1.0, demand: progress);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(7, ShellItemIconLocationMode.FileType);
        loader.CompleteNextShellOwner(CreateTestIconSource());

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(99, ShellItemIconLocationMode.ExactItem);
        var exactSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(exactSource);

        Assert.AreSame(exactSource, await finalTask);
        progress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ReleasedProgressiveRequestDoesNotDemandItsDelayedExactLoad()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest();
        var icon = new IconDataViewModel
        {
            Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\custom.exe"),
        };

        var finalTask = provider.GetIconSource(icon, 1.0, demand: progress);
        progress.Release();
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(7, ShellItemIconLocationMode.FileType);
        loader.CompleteNextShellOwner(CreateTestIconSource());

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2,
            TimeSpan.FromSeconds(2)));
        Assert.IsNotNull(loader.LastDemand);
        Assert.IsFalse(loader.LastDemand.IsDemanded);

        loader.LocateNextShellItem(99, ShellItemIconLocationMode.ExactItem);
        loader.CompleteNextShellOwner(CreateTestIconSource());
        await finalTask;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ColdTypeMissMovesCacheArbitrationOffCallingThread()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var progress = new ProgressiveIconRequest();
        var callingThreadId = Environment.CurrentManagedThreadId;

        var result = provider.GetIconSource(
            new IconDataViewModel
            {
                Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\first.dll"),
            },
            1.0,
            demand: progress);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        Assert.AreNotEqual(callingThreadId, loader.LastShellEnqueueThreadId);

        loader.LocateNextShellItem(42, ShellItemIconLocationMode.FileType);
        var typeSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(typeSource);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(42, ShellItemIconLocationMode.ExactItem);
        Assert.AreSame(typeSource, await result);
        progress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CachedTypeHitMovesExactArbitrationOffCallingSynchronizationContext()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var firstProgress = new ProgressiveIconRequest();
        var first = provider.GetIconSource(
            new IconDataViewModel
            {
                Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\first.dll"),
            },
            1.0,
            demand: firstProgress);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 1,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(42, ShellItemIconLocationMode.FileType);
        var sharedTypeSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(sharedTypeSource);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 2,
            TimeSpan.FromSeconds(2)));
        loader.LocateNextShellItem(42, ShellItemIconLocationMode.ExactItem);
        Assert.AreSame(sharedTypeSource, await first);
        firstProgress.Release();

        var originalContext = SynchronizationContext.Current;
        var callingThreadId = Environment.CurrentManagedThreadId;
        var secondProgress = new ProgressiveIconRequest();
        Task<IconSource?> second;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        try
        {
            second = provider.GetIconSource(
                new IconDataViewModel
                {
                    Icon = ShellItemIconProtocol.Create(@"C:\Windows\System32\second.dll"),
                },
                1.0,
                demand: secondProgress);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        Assert.IsTrue(SpinWait.SpinUntil(
            () => loader.ShellEnqueueCount == 3,
            TimeSpan.FromSeconds(2)));
        Assert.AreNotEqual(callingThreadId, loader.LastExactShellEnqueueThreadId);
        loader.LocateNextShellItem(42, ShellItemIconLocationMode.ExactItem);
        Assert.AreSame(sharedTypeSource, await second);
        secondProgress.Release();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CanonicalJoinPinsExistingLoadDemanded()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var firstRequestDemand = new IconRequestDemand();
        var secondRequestDemand = new IconRequestDemand();

        var first = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\first.txt" },
            1.0,
            demand: firstRequestDemand);
        var canonicalDemand = loader.LastDemand!;
        var second = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\second.txt" },
            1.0,
            demand: secondRequestDemand);

        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        firstRequestDemand.Release();
        secondRequestDemand.Release();

        Assert.IsTrue(
            canonicalDemand.IsDemanded,
            "The canonical load must remain demanded after a raw load joins it.");

        loader.CompleteNextShellOwner(CreateTestIconSource());
        await Task.WhenAll(first, second);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task NullCanonicalShellResultIsNotCached()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "C:\\Files\\report.txt" };

        var first = provider.GetIconSource(icon, 1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.CompleteNextShellOwner(null);
        Assert.IsNull(await first);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));
        Assert.AreEqual(0, GetCacheCount(provider, "_otherCache"));

        var retry = provider.GetIconSource(icon, 1.0);
        Assert.AreEqual(2, loader.ShellExtractionCount);
        loader.CompleteNextShellOwner(CreateTestIconSource());
        await retry;
    }

    [TestMethod]
    [DoNotParallelize]
    [Timeout(5_000)]
    public async Task SharedShellFallbackIsNotCachedAsCanonicalResult()
    {
        var fallback = CreateTestIconSource();
        var sourceField = typeof(ShellItemIconFallback).GetField(
            "_source",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousSource = sourceField.GetValue(null);
        sourceField.SetValue(null, fallback);
        try
        {
            var loader = new ControllableIconLoader();
            var provider = CreateProvider(loader);
            var icon = new IconDataViewModel { Icon = "C:\\Files\\report.txt" };

            var first = provider.GetIconSource(icon, 1.0);
            loader.LocateNextShellItem(systemImageListIndex: 42);
            loader.CompleteNextShellOwner(fallback);
            Assert.AreSame(fallback, await first);
            Assert.IsTrue(
                SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));
            Assert.AreEqual(0, GetCacheCount(provider, "_otherCache"));

            var retry = provider.GetIconSource(icon, 1.0);
            Assert.AreEqual(2, loader.ShellExtractionCount);
            loader.CompleteNextShellOwner(CreateTestIconSource());
            await retry;
        }
        finally
        {
            sourceField.SetValue(null, previousSource);
        }
    }

    [DataTestMethod]
    [DataRow("C:\\Files\\image.avif")]
    [DataRow("C:\\Files\\image.heic")]
    [DataRow("C:\\Files\\image.jfif")]
    [Timeout(5_000)]
    public async Task LegacyImagePathsPreserveOrdinaryImageLoading(string imagePath)
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);

        var load = provider.GetIconSource(new IconDataViewModel { Icon = imagePath }, 1.0);

        Assert.AreEqual(1, loader.EnqueueCount);
        Assert.AreEqual(0, loader.ShellEnqueueCount);
        loader.CompleteNext(null);
        await load;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ExplicitAndLegacyShellRequestsShareCanonicalLoad()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var legacy = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\legacy.txt" },
            1.0);
        var explicitRequest = provider.GetIconSource(
            new IconDataViewModel
            {
                Icon = ShellItemIconProtocol.Create("C:\\Files\\explicit.txt"),
            },
            1.0);

        loader.LocateNextShellItem(systemImageListIndex: 73);
        loader.LocateNextShellItem(systemImageListIndex: 73);
        Assert.AreEqual(1, loader.ShellExtractionCount);

        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);
        Assert.AreSame(source, await legacy);
        Assert.AreSame(source, await explicitRequest);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ExplicitShellProtocolOverridesCompanionStream()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var load = provider.GetIconSource(
            new IconDataViewModel
            {
                Icon = ShellItemIconProtocol.Create("C:\\Files\\report.txt"),
                Data = new IconDataStreamReference { Unsafe = new TestStreamReference() },
            },
            1.0);

        Assert.AreEqual(0, loader.EnqueueCount);
        Assert.AreEqual(1, loader.ShellEnqueueCount);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);

        Assert.AreSame(source, await load);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DistinctShellIdentitiesDoNotShareMaterialization()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var first = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\first.txt" },
            1.0);
        var second = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\second.custom" },
            1.0);

        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.LocateNextShellItem(systemImageListIndex: 99);
        Assert.AreEqual(2, loader.ShellExtractionCount);

        var firstSource = CreateTestIconSource();
        var secondSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(firstSource);
        loader.CompleteNextShellOwner(secondSource);
        Assert.AreSame(firstSource, await first);
        Assert.AreSame(secondSource, await second);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task FailedCanonicalShellLoadCanRetryWithoutRelocatingRawPath()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "C:\\Files\\report.txt" };

        var failed = provider.GetIconSource(icon, 1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.FailNextShellOwner(new IOException("Extraction failed."));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await failed);
        Assert.IsTrue(SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));

        var retry = provider.GetIconSource(icon, 1.0);
        Assert.AreEqual(1, loader.ShellLocationCount);
        Assert.AreEqual(2, loader.ShellExtractionCount);

        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);
        Assert.AreSame(source, await retry);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ShellLocationAliasIsSharedAcrossSizeProviders()
    {
        var loader = new ControllableIconLoader();
        var provider20 = new CachedIconSourceProvider(
            loader,
            new Size(20, 20),
            glyphCacheSize: 16,
            otherCacheSize: 16);
        var provider64 = new CachedIconSourceProvider(
            loader,
            new Size(64, 64),
            glyphCacheSize: 16,
            otherCacheSize: 16);
        var icon = new IconDataViewModel { Icon = "C:\\Files\\report.txt" };

        var small = provider20.GetIconSource(icon, 1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        loader.CompleteNextShellOwner(CreateTestIconSource());
        await small;

        var large = provider64.GetIconSource(icon, 1.0);
        Assert.AreEqual(1, loader.ShellLocationCount);
        Assert.AreEqual(2, loader.ShellExtractionCount);
        loader.CompleteNextShellOwner(CreateTestIconSource());
        await large;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ShellLocationInvalidationSeparatesReusedImageListIndex()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "C:\\Files\\report.txt" };

        var first = provider.GetIconSource(icon, 1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        var firstSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(firstSource);
        Assert.AreSame(firstSource, await first);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)));

        loader.ShellIconLocations.Clear();

        var refreshed = provider.GetIconSource(icon, 1.0);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        Assert.AreEqual(2, loader.ShellExtractionCount);
        var refreshedSource = CreateTestIconSource();
        loader.CompleteNextShellOwner(refreshedSource);
        Assert.AreSame(refreshedSource, await refreshed);
        Assert.AreNotSame(firstSource, refreshedSource);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task FailedLoadIsRemovedAndCanBeRetried()
    {
        var loader = new ControllableIconLoader();
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "test" };

        var failed = provider.GetIconSource(icon, 1.0);
        loader.FailNext(new InvalidOperationException("Icon load failed."));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await failed);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)),
            "The failed load was not retired from the in-flight dictionary.");

        var retry = provider.GetIconSource(icon, 1.0);

        Assert.AreNotSame(failed, retry);
        Assert.AreEqual(2, loader.EnqueueCount);

        loader.CompleteNext(null);
        await retry;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RejectedLoadFaultsAndCanBeRetried()
    {
        var loader = new ControllableIconLoader { AcceptLoads = false };
        var provider = CreateProvider(loader);
        var icon = new IconDataViewModel { Icon = "test" };

        var rejected = provider.GetIconSource(icon, 1.0);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await rejected);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => GetInFlightCount(provider) == 0, TimeSpan.FromSeconds(2)),
            "The rejected load was not retired from the in-flight dictionary.");

        loader.AcceptLoads = true;
        var retry = provider.GetIconSource(icon, 1.0);

        Assert.AreNotSame(rejected, retry);
        Assert.AreEqual(2, loader.EnqueueCount);

        loader.CompleteNext(null);
        await retry;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UncachedProviderFaultsRejectedLoad()
    {
        var loader = new ControllableIconLoader { AcceptLoads = false };
        var provider = new IconSourceProvider(loader, new Size(16, 16));

        var rejected = provider.GetIconSource(new IconDataViewModel { Icon = "test" }, 1.0);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await rejected);
        Assert.AreEqual(1, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UncachedProviderLoadsGlyphDirectlyWithoutQueueing()
    {
        var glyph = CreateTestIconSource();
        var loader = new ControllableIconLoader
        {
            ReturnDirectGlyph = true,
            DirectGlyphResult = glyph,
        };
        var provider = new IconSourceProvider(loader, new Size(16, 16));

        var result = await provider.GetIconSource(new IconDataViewModel { Icon = "\uE700" }, 1.0);

        Assert.AreSame(glyph, result);
        Assert.AreEqual(1, loader.GlyphAttemptCount);
        Assert.AreEqual(0, loader.EnqueueCount);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UncachedProviderRoutesLegacyPathsThroughShellLoading()
    {
        var loader = new ControllableIconLoader();
        var provider = new IconSourceProvider(loader, new Size(16, 16));

        var load = provider.GetIconSource(
            new IconDataViewModel { Icon = "C:\\Files\\report.txt" },
            1.0);

        Assert.AreEqual(0, loader.EnqueueCount);
        Assert.AreEqual(1, loader.ShellEnqueueCount);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);

        Assert.AreSame(source, await load);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UncachedProviderLetsExplicitShellProtocolOverrideCompanionStream()
    {
        var loader = new ControllableIconLoader();
        var provider = new IconSourceProvider(loader, new Size(16, 16));
        var load = provider.GetIconSource(
            new IconDataViewModel
            {
                Icon = ShellItemIconProtocol.Create("C:\\Files\\report.txt"),
                Data = new IconDataStreamReference { Unsafe = new TestStreamReference() },
            },
            1.0);

        Assert.AreEqual(0, loader.EnqueueCount);
        Assert.AreEqual(1, loader.ShellEnqueueCount);
        loader.LocateNextShellItem(systemImageListIndex: 42);
        var source = CreateTestIconSource();
        loader.CompleteNextShellOwner(source);

        Assert.AreSame(source, await load);
    }

    [TestMethod]
    public void UncachedProviderClassifiesSharedLegacyLocationAliasAsHit()
    {
        var loader = new ControllableIconLoader();
        var provider = new IconSourceProvider(loader, new Size(16, 16));
        var request = new ShellItemIconRequest("C:\\Files\\report.txt", jumbo: false);
        var resolved = new LocatedShellIcon(
            request,
            ShellIconIdentity.FromSystemImageList(42, jumbo: false));
        Assert.IsTrue(
            loader.ShellIconLocations.TryAdd(
                request,
                resolved,
                loader.ShellIconLocations.Generation,
                out var cachedLocation));

        Assert.IsTrue(provider.TryGetShellItemRequest(
            request.ItemPath,
            out var classifiedRequest,
            out var locatedIcon,
            out var locationCacheHit));
        Assert.AreEqual(request, classifiedRequest);
        Assert.IsNotNull(locatedIcon);
        Assert.AreEqual(cachedLocation, locatedIcon.Value);
        Assert.IsTrue(locationCacheHit);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UncachedProviderFallsBackWhenLoaderViolatesDirectGlyphContract()
    {
        var loader = new ControllableIconLoader { ReturnDirectGlyph = true };
        var provider = new IconSourceProvider(loader, new Size(16, 16));

        var result = provider.GetIconSource(new IconDataViewModel { Icon = "glyph" }, 1.0);

        Assert.AreEqual(1, loader.GlyphAttemptCount);
        Assert.AreEqual(1, loader.EnqueueCount);
        loader.CompleteNext(null);
        Assert.IsNull(await result);
    }

    private static IconSource CreateTestIconSource()
    {
        // The unit-test process does not initialize WinUI. Providers treat a completed
        // IconSource opaquely, so use a non-activated projection only as an identity token.
        return (IconSource)RuntimeHelpers.GetUninitializedObject(typeof(FontIconSource));
    }

    private static int GetInFlightCount(CachedIconSourceProvider provider)
    {
        var field = typeof(CachedIconSourceProvider).GetField("_inFlight", BindingFlags.Instance | BindingFlags.NonPublic);
        var inFlight = field!.GetValue(provider)!;
        var countProperty = inFlight.GetType().GetProperty("Count");
        return (int)countProperty!.GetValue(inFlight)!;
    }

    private static int GetCacheCount(CachedIconSourceProvider provider, string fieldName)
    {
        var field = typeof(CachedIconSourceProvider).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var cache = field!.GetValue(provider)!;
        var countProperty = cache.GetType().GetProperty("ApproximateCount", BindingFlags.Instance | BindingFlags.NonPublic);
        return (int)countProperty!.GetValue(cache)!;
    }

    private static CachedIconSourceProvider CreateProvider(
        ControllableIconLoader loader,
        int glyphCacheSize = 16,
        int otherCacheSize = 16) =>
        new(loader, new Size(20, 20), glyphCacheSize, otherCacheSize);

    private static (TestStreamReference First, TestStreamReference Second)? FindRuntimeHashCollision()
    {
        const int MaximumCandidates = 500_000;
        var referencesByHash = new Dictionary<int, TestStreamReference>();

        for (var i = 0; i < MaximumCandidates; i++)
        {
            var candidate = new TestStreamReference();
            var hash = RuntimeHelpers.GetHashCode(candidate);
            if (referencesByHash.TryGetValue(hash, out var existing))
            {
                return (existing, candidate);
            }

            referencesByHash.Add(hash, candidate);
        }

        return null;
    }

    private sealed partial class TestStreamReference : IRandomAccessStreamReference
    {
        public IAsyncOperation<IRandomAccessStreamWithContentType> OpenReadAsync() =>
            throw new NotSupportedException("The cache-key test does not open its stream references.");
    }

    private sealed class ControllableIconLoader : IIconLoaderService
    {
        private readonly ConcurrentQueue<TaskCompletionSource<IconSource?>> _pending = new();
        private readonly ConcurrentQueue<PendingShellLoad> _pendingShellLocations = new();
        private readonly ConcurrentQueue<PendingShellLoad> _pendingShellOwners = new();
        private int _enqueueCount;
        private int _glyphAttemptCount;
        private int _shellEnqueueCount;
        private int _shellExtractionCount;
        private int _shellLocationCount;
        private int _lastShellEnqueueThreadId;
        private int _lastExactShellEnqueueThreadId;

        public bool AcceptLoads { get; set; } = true;

        public bool ReturnDirectGlyph { get; set; }

        public IconSource? DirectGlyphResult { get; set; }

        public int EnqueueCount => Volatile.Read(ref _enqueueCount);

        public int GlyphAttemptCount => Volatile.Read(ref _glyphAttemptCount);

        public int ShellEnqueueCount => Volatile.Read(ref _shellEnqueueCount);

        public int ShellExtractionCount => Volatile.Read(ref _shellExtractionCount);

        public int ShellLocationCount => Volatile.Read(ref _shellLocationCount);

        public int LastShellEnqueueThreadId => Volatile.Read(ref _lastShellEnqueueThreadId);

        public int LastExactShellEnqueueThreadId => Volatile.Read(ref _lastExactShellEnqueueThreadId);

        public IconLoadDemand? LastDemand { get; private set; }

        public ShellIconLocationCache ShellIconLocations { get; } = new();

        public bool TryLoadGlyph(
            string? iconString,
            string? fontFamily,
            Size iconSize,
            double scale,
            [MaybeNullWhen(false)] out IconSource result)
        {
            Interlocked.Increment(ref _glyphAttemptCount);
            result = DirectGlyphResult!;
            return ReturnDirectGlyph;
        }

        public bool TryEnqueueLoad(
            string? iconString,
            string? fontFamily,
            IRandomAccessStreamReference? streamRef,
            Size iconSize,
            double scale,
            ElementTheme theme,
            TaskCompletionSource<IconSource?> tcs,
            IconLoadPriority priority,
            IconLoadMeasurement? diagnostics = null,
            IconLoadDemand? demand = null)
        {
            _ = theme;
            Interlocked.Increment(ref _enqueueCount);
            LastDemand = demand;
            if (!AcceptLoads)
            {
                return false;
            }

            _pending.Enqueue(tcs);
            return true;
        }

        public bool TryEnqueueShellItemLoad(
            ShellItemIconRequest request,
            LocatedShellIcon? locatedIcon,
            Size iconSize,
            double scale,
            TaskCompletionSource<IconSource?> tcs,
            IconLoadPriority priority,
            IconLoadMeasurement? diagnostics = null,
            IconLoadDemand? demand = null,
            IShellItemIconLoadCoordinator? coordinator = null,
            ShellIconMeasurement shellDiagnostics = default)
        {
            _ = shellDiagnostics;
            _ = iconSize;
            _ = scale;
            _ = priority;
            _ = diagnostics;
            Interlocked.Increment(ref _shellEnqueueCount);
            Volatile.Write(ref _lastShellEnqueueThreadId, Environment.CurrentManagedThreadId);
            if (request.LocationMode == ShellItemIconLocationMode.ExactItem)
            {
                Volatile.Write(ref _lastExactShellEnqueueThreadId, Environment.CurrentManagedThreadId);
            }

            LastDemand = demand;
            if (!AcceptLoads)
            {
                return false;
            }

            var load = new PendingShellLoad(request, tcs, coordinator);
            if (locatedIcon is null)
            {
                _pendingShellLocations.Enqueue(load);
            }
            else
            {
                Interlocked.Increment(ref _shellExtractionCount);
                _pendingShellOwners.Enqueue(load);
            }

            return true;
        }

        public void CompleteNext(IconSource? result)
        {
            Assert.IsTrue(_pending.TryDequeue(out var tcs), "No pending icon load was available.");
            tcs.SetResult(result);
        }

        public void FailNext(Exception exception)
        {
            Assert.IsTrue(_pending.TryDequeue(out var tcs), "No pending icon load was available.");
            tcs.SetException(exception);
        }

        public void LocateNextShellItem(
            int systemImageListIndex,
            ShellItemIconLocationMode? expectedLocationMode = null)
        {
            Assert.IsTrue(
                _pendingShellLocations.TryDequeue(out var load),
                "No Shell item was waiting for identity resolution.");
            if (expectedLocationMode is not null)
            {
                Assert.AreEqual(expectedLocationMode.Value, load.Request.LocationMode);
            }

            Interlocked.Increment(ref _shellLocationCount);
            var locatedIcon = new LocatedShellIcon(
                load.Request,
                ShellIconIdentity.FromSystemImageList(
                    systemImageListIndex,
                    load.Request.Jumbo));
            Assert.IsTrue(
                ShellIconLocations.TryAdd(
                    load.Request,
                    locatedIcon,
                    ShellIconLocations.Generation,
                    out var cachedLocation));
            if (load.Coordinator?.TryJoinExistingLoad(cachedLocation, out var sharedTask) == true)
            {
                Forward(sharedTask, load.Completion);
                return;
            }

            Interlocked.Increment(ref _shellExtractionCount);
            _pendingShellOwners.Enqueue(load);
        }

        public void CompleteNextShellOwner(IconSource? result)
        {
            Assert.IsTrue(
                _pendingShellOwners.TryDequeue(out var load),
                "No canonical Shell icon load was available.");
            load.Completion.SetResult(result);
        }

        public void FailNextShellOwner(Exception exception)
        {
            Assert.IsTrue(
                _pendingShellOwners.TryDequeue(out var load),
                "No canonical Shell icon load was available.");
            load.Completion.SetException(exception);
        }

        private static void Forward(
            Task<IconSource?> sharedTask,
            TaskCompletionSource<IconSource?> completion)
        {
            _ = sharedTask.ContinueWith(
                completed =>
                {
                    if (completed.IsCompletedSuccessfully)
                    {
                        completion.TrySetResult(completed.Result);
                    }
                    else if (completed.IsCanceled)
                    {
                        completion.TrySetCanceled();
                    }
                    else
                    {
                        completion.TrySetException(completed.Exception!.InnerExceptions);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class PendingShellLoad
        {
            public PendingShellLoad(
                ShellItemIconRequest request,
                TaskCompletionSource<IconSource?> completion,
                IShellItemIconLoadCoordinator? coordinator)
            {
                Request = request;
                Completion = completion;
                Coordinator = coordinator;
            }

            public ShellItemIconRequest Request { get; }

            public TaskCompletionSource<IconSource?> Completion { get; }

            public IShellItemIconLoadCoordinator? Coordinator { get; }
        }
    }

    private sealed class ProgressiveIconRequest : IIconRequestDemand, IIconRequestProgress
    {
        private IconRequestDemandState _demandState;
        private IconSource? _intermediate;

        public IconSource? Intermediate => Volatile.Read(ref _intermediate);

        public bool ThrowOnIntermediate { get; set; }

        void IIconRequestDemand.Attach(IconLoadDemand loadDemand) => _demandState.Attach(loadDemand);

        public void Release() => _demandState.Release();

        bool IIconRequestProgress.TryReportIntermediate(IconSource source, Action<bool>? presentationCompleted)
        {
            if (ThrowOnIntermediate)
            {
                throw new InvalidOperationException("The test requestor rejected the intermediate source.");
            }

            Volatile.Write(ref _intermediate, source);
            presentationCompleted?.Invoke(true);
            return true;
        }
    }
}
