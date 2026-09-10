// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public sealed partial class PageCleanupTests
{
    [TestMethod]
    public async Task ContentInitializationFailureReleasesTheWholeUnpublishedBatch()
    {
        var scheduler = new QueuedTaskScheduler();
        var firstContent = new Mock<IMarkdownContent>();
        var failingContent = new Mock<IMarkdownContent>();
        var failure = new InvalidOperationException("Failed to read markdown content.");
        failingContent.SetupGet(c => c.Body).Throws(failure);
        var page = new Mock<IContentPage>();
        page.Setup(p => p.GetContent()).Returns([firstContent.Object, failingContent.Object]);
        var viewModel = new CommandPaletteContentPageViewModel(page.Object, scheduler, new TestHost(), CommandProviderContext.Empty);
        var publications = 0;
        viewModel.Content.CollectionChanged += (_, _) => publications++;

        try
        {
            Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(viewModel.InitializeProperties));
            scheduler.Drain();
            Assert.AreEqual(0, publications);
            Assert.AreEqual(0, viewModel.Content.Count);
        }
        finally
        {
            await viewModel.CleanupAsync().WaitAsync(TestTimeout);
        }

        Assert.IsTrue(viewModel.IsDiscarded);
        firstContent.VerifyAdd(c => c.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        failingContent.VerifyGet(c => c.Body, Times.Once);
        failingContent.VerifyAdd(c => c.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        firstContent.VerifyRemove(c => c.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        failingContent.VerifyRemove(c => c.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CleanupCompletesAndRemainsTerminalWhenTeardownThrows(bool failStopHook)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new QueuedTaskScheduler();
        var page = new Mock<IPage>();
        TypedEventHandler<object, IPropChangedEventArgs>? notification = null;
        page.SetupAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(handler => notification = handler);
        if (!failStopHook)
        {
            page.SetupRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
                .Throws(new InvalidOperationException("Extension disconnected during cleanup."));
        }

        var viewModel = new CleanupFailurePageViewModel(page.Object, scheduler, failStopHook);
        viewModel.InitializeProperties();
        Assert.IsNotNull(notification);
        page.SetupGet(p => p.Title).Returns(() =>
        {
            Block(entered, release);
            return "Updated title";
        });

        var update = Task.Run(() => notification(page.Object, new PropChangedEventArgs(nameof(IPage.Title))));
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            cleanup = viewModel.CleanupAsync();
            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(cleanup.IsCompleted);
            Assert.AreSame(cleanup, viewModel.CleanupAsync());
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await update.WaitAsync(TestTimeout);
            await (cleanup ?? viewModel.CleanupAsync()).WaitAsync(TestTimeout);
        }

        Assert.AreSame(cleanup, viewModel.CleanupAsync());
        await viewModel.ResumeAfterNavigation().WaitAsync(TestTimeout);
        viewModel.InitializeProperties();
        notification(page.Object, new PropChangedEventArgs(nameof(IPage.Title)));
        scheduler.Drain();

        Assert.IsTrue(viewModel.IsDiscarded);
        Assert.IsFalse(viewModel.IsActive);
        Assert.AreEqual(1, viewModel.CleanupRequestCount);
        page.VerifyGet(p => p.Title, Times.Exactly(2));
        page.VerifyAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    private sealed partial class CleanupFailurePageViewModel(IPage page, TaskScheduler scheduler, bool failStopHook)
        : PageViewModel(page, scheduler, new TestHost(), CommandProviderContext.Empty)
    {
        internal bool IsActive => IsPageActive;

        internal int CleanupRequestCount { get; private set; }

        protected override void OnCleanupRequested()
        {
            CleanupRequestCount++;
            if (failStopHook)
            {
                throw new InvalidOperationException("Failed to stop page work.");
            }
        }
    }
}
