// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SamplePagesExtension;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ComposedContentViewModelTests
{
    [TestMethod]
    public void ContentDetails_IsOptIn_AndDoesNotReadLegacyPresentation()
    {
        using var context = new TestContext();
        var details = new ContentOnlyDetails { Content = [new HeaderContent { Title = "New header", Subtitle = "Subtitle" }] };
        var vm = context.Own(new DetailsViewModel(details, context.Reference));
        vm.InitializeProperties();
        context.Drain();

        Assert.IsFalse(new Details() is IDetails2);
        Assert.IsTrue(vm.IsContentOnly);
        Assert.AreEqual("New header", vm.Title);
        Assert.AreEqual(1, vm.Content.Count);
        Assert.AreEqual("Subtitle", ((ContentHeaderViewModel)vm.Content[0]).Subtitle);
        Assert.AreEqual(0, vm.Metadata.Count);

        var header = (HeaderContent)details.Content[0];
        header.Title = "Updated header";
        header.Subtitle = string.Empty;
        vm.Content[0].ApplyPendingUpdates();
        context.Drain();
        Assert.AreEqual("Updated header", vm.Title);
        Assert.AreEqual(string.Empty, vm.Body);
    }

    [TestMethod]
    public void TextContent_IsDistinctFromDocumentText_AndStopsObservingAfterRemoval()
    {
        using var context = new TestContext();
        var text = new TextContent("Compact value");
        var document = new PlainTextContent("Document text");
        var owner = context.Own(new ContentCollectionViewModel(context.Reference));
        owner.Update([text, document]);
        context.Drain();

        var textVm = (ContentTextViewModel)owner.Items[0];
        Assert.IsInstanceOfType<ContentPlainTextViewModel>(owner.Items[1]);
        Assert.AreEqual("Compact value", textVm.Text);

        text.Text = "<tag> & **literal text**";
        textVm.ApplyPendingUpdates();
        Assert.AreEqual("<tag> & **literal text**", textVm.Text);

        owner.Update([document]);
        context.Drain();
        text.Text = "No longer observed";
        Assert.AreEqual("<tag> & **literal text**", textVm.Text);
    }

    [TestMethod]
    public void ImageBeforeHeader_PreservesOrder_AndStillNamesDetails()
    {
        using var context = new TestContext();
        var image = new ImageContent(new IconInfo("\uE91B")) { MaxHeight = 240 };
        var header = new HeaderContent { Title = "Image preview", Subtitle = "Header after image" };
        var details = new ContentOnlyDetails
        {
            Content =
            [
                image,
                header,
                new PropertyGridContent
                {
                    Properties = [new PropertyContent { Label = "Format", Value = new TextContent("JPEG") }],
                },
            ],
        };
        var vm = context.Own(new DetailsViewModel(details, context.Reference));
        vm.InitializeProperties();
        context.Drain();

        Assert.AreEqual(3, vm.Content.Count);
        var imageVm = (ContentImageViewModel)vm.Content[0];
        Assert.AreSame(image, imageVm.Model.Unsafe);
        Assert.AreEqual(240d, imageVm.MaxHeight);
        Assert.IsInstanceOfType<ContentHeaderViewModel>(vm.Content[1]);
        Assert.IsInstanceOfType<ContentPropertyGridViewModel>(vm.Content[2]);
        Assert.AreEqual("Image preview", vm.Title);
        Assert.AreEqual("Header after image", vm.Body);

        header.Title = "Updated image title";
        vm.Content[1].ApplyPendingUpdates();
        context.Drain();
        Assert.AreEqual("Updated image title", vm.Title);
        Assert.AreSame(imageVm, vm.Content[0]);
    }

    [TestMethod]
    public void BackgroundSnapshots_PublishOnlyOnUiScheduler_AndKeepIdentity()
    {
        using var context = new TestContext();
        var model = new MarkdownContent("First");
        var owner = context.Own(new ContentCollectionViewModel(context.Reference));
        var notifications = new List<int>();
        owner.Items.CollectionChanged += (_, _) => notifications.Add(Environment.CurrentManagedThreadId);

        Task.Run(() => owner.Update([model])).GetAwaiter().GetResult();
        Assert.AreEqual(0, notifications.Count);
        context.Drain();
        var original = owner.Items.Single();

        Task.Run(() => owner.Update([model, new SeparatorContent()])).GetAwaiter().GetResult();
        context.Drain();
        Assert.AreSame(original, owner.Items[0]);
        Assert.IsTrue(notifications.All(id => id == Environment.CurrentManagedThreadId));

        owner.Update([]);
        context.Drain();
        model.Body = "After removal";
        Assert.AreEqual("First", ((ContentMarkdownViewModel)original).Body);
    }

    [TestMethod]
    public void SectionPreview_CountsDirectChildren_AndKeepsExpansionOnRefresh()
    {
        using var context = new TestContext();
        var grid = new PropertyGridContent
        {
            Properties =
            [
                new PropertyContent { Label = "One", Value = new MarkdownContent("First") },
                new PropertyContent { Label = "Two", Value = new LinkContent { Link = new Uri("https://example.com") } },
            ],
        };
        var section = new SectionContent
        {
            PreviewItemCount = 1,
            Content = [grid, new MarkdownContent("Second"), new SeparatorContent()],
        };
        var owner = context.Own(new ContentCollectionViewModel(context.Reference));
        owner.Update([section]);
        context.Drain();
        var vm = (ContentSectionViewModel)owner.Items.Single();
        Assert.AreEqual(1, vm.VisibleContent.Count);
        Assert.IsInstanceOfType<ContentPropertyGridViewModel>(vm.VisibleContent[0]);
        Assert.AreEqual(2, vm.HiddenItemCount);

        vm.IsExpanded = true;
        Assert.AreEqual(3, vm.VisibleContent.Count);
        section.Content = [.. section.Content, new MarkdownContent("Fourth")];
        owner.Update([section]);
        context.Drain();
        Assert.AreSame(vm, owner.Items.Single());
        Assert.IsTrue(vm.IsExpanded);
        Assert.AreEqual(4, vm.VisibleContent.Count);

        vm.IsExpanded = false;
        Assert.AreEqual(1, vm.VisibleContent.Count);
        section.PreviewItemCount = 0;
        context.Drain();
        Assert.AreEqual(0, vm.VisibleContent.Count);
        Assert.IsTrue(vm.CanExpand);

        section.PreviewItemCount = -1;
        context.Drain();
        Assert.AreEqual(4, vm.VisibleContent.Count);
        Assert.IsFalse(vm.CanExpand);
        section.PreviewItemCount = 4;
        context.Drain();
        Assert.IsFalse(vm.CanExpand);
    }

    [TestMethod]
    public void RichPropertyValue_UpdatesAndCleansReplacedContent()
    {
        using var context = new TestContext();
        var link = new LinkContent { Link = new Uri("file:///C:/Windows") };
        var property = new PropertyContent { Label = "Folder", Value = link };
        var vm = context.Own(new ContentPropertyViewModel(property, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        var old = (ContentLinkViewModel)vm.ValueContent.Single();
        Assert.AreEqual(link.Link.ToString(), old.Text);

        property.Value = new TagsContent { Tags = [new Tag("New value") { ToolTip = "Tooltip" }] };
        context.Drain();
        Assert.AreEqual("Tooltip", ((ContentTagsViewModel)vm.ValueContent.Single()).Tags.Single().ToolTip);
        link.Text = "No longer observed";
        Assert.AreEqual("file:///C:/Windows", old.Text);
    }

    [TestMethod]
    public void TreeRootReplacement_InitializesNewRoot_AndAllowsNull()
    {
        using var context = new TestContext();
        var old = new MarkdownContent("Original");
        var model = new TreeContent { RootContent = old, Children = [new HeaderContent { Title = "Child header" }] };
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        var oldVm = (ContentMarkdownViewModel)vm.Root.Single();

        model.RootContent = new LinkContent { Text = "Replacement" };
        context.Drain();
        Assert.AreEqual("Replacement", ((ContentLinkViewModel)vm.Root.Single()).Text);
        old.Body = "Removed";
        Assert.AreEqual("Original", oldVm.Body);

        model.RootContent = null;
        context.Drain();
        Assert.AreEqual(0, vm.Root.Count);
        Assert.IsTrue(vm.HasChildren);
    }

    [TestMethod]
    public void TreeCleanup_RejectsReentrantAndPreviouslyCapturedCallbacks()
    {
        using var context = new TestContext();
        var model = new SubscriptionTree();
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));
        vm.InitializeProperties();
        var lateItemsChanged = model.CaptureItemsChanged();
        model.OnItemsUnsubscribe = model.NotifyRootChanged;
        var reads = model.ChildrenReads;

        vm.SafeCleanup();
        lateItemsChanged();
        vm.InitializeProperties();
        vm.SafeCleanup();
        context.Drain();

        Assert.AreEqual(1, model.ItemsAdds);
        Assert.AreEqual(1, model.ItemsRemoves);
        Assert.AreEqual(0, model.ItemsSubscribers);
        Assert.AreEqual(0, model.PropertySubscribers);
        Assert.AreEqual(reads, model.ChildrenReads);
    }

    [TestMethod]
    public void TreeCleanup_DuringEventRegistration_RevokesTheLateSubscription()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new SubscriptionTree
        {
            OnItemsSubscribe = () =>
            {
                entered.Set();
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
            },
        };
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));
        var pending = Task.Run(vm.InitializeProperties);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            vm.SafeCleanup();
            model.NotifyRootChanged();
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        Assert.AreEqual(1, model.ItemsAdds);
        Assert.AreEqual(1, model.ItemsRemoves);
        Assert.AreEqual(0, model.ItemsSubscribers);
        Assert.AreEqual(0, model.PropertySubscribers);
        Assert.AreEqual(0, model.ChildrenReads);
    }

    [TestMethod]
    public void TreeInitialization_ObservesItemsOnceDuringConcurrentRefresh()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new SubscriptionTree
        {
            OnItemsSubscribe = () =>
            {
                entered.Set();
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
            },
        };
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));
        var pending = Task.Run(vm.InitializeProperties);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            Parallel.For(0, 16, _ =>
            {
                vm.InitializeProperties();
                model.NotifyRootChanged();
            });
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        Assert.AreEqual(1, model.ItemsAdds);
        Assert.AreEqual(1, model.ItemsSubscribers);
        Assert.AreEqual(1, model.PropertySubscribers);
        vm.SafeCleanup();
        Assert.AreEqual(0, model.ItemsSubscribers);
        Assert.AreEqual(0, model.PropertySubscribers);
    }

    [TestMethod]
    public void TreeSubscriptionFailure_RevokesEarlierRegistrations()
    {
        using var context = new TestContext();
        var model = new SubscriptionTree
        {
            OnItemsSubscribe = () => throw new InvalidOperationException("Expected registration failure"),
        };
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));

        Assert.ThrowsException<InvalidOperationException>(vm.InitializeProperties);
        Assert.AreEqual(0, model.ItemsSubscribers);
        Assert.AreEqual(0, model.PropertySubscribers);
        vm.InitializeProperties();
        Assert.AreEqual(1, model.ItemsAdds);
    }

    [TestMethod]
    public void DetailsReplacement_RetainsDisplayedContentThroughLoadingAndHandoff()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var text = new TextContent("Original");
        var model = new TestContentPage { Details = new ContentDetails { Content = [text] } };
        var page = context.CreateContentPage(model);
        var shell = context.CreateShell();
        var original = page.Details!;
        var originalText = (ContentTextViewModel)original.Content.Single();
        Assert.IsTrue(shell.TrySetDetails(original));
        Assert.IsTrue(shell.TrySetDetails(original));
        var requests = context.CaptureDetailsRequests();
        var candidate = new CallbackDetails(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        })
        {
            Content = [new TextContent("Replacement")],
        };
        var pending = Task.Run(() => model.Details = candidate);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            context.Drain();
            Assert.AreSame(original, page.Details);
            Assert.AreSame(original, shell.Details);
            Assert.AreEqual(1, original.Content.Count);
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        var replacement = requests.Single();
        Assert.AreSame(page.Details, replacement);
        Assert.AreSame(original, shell.Details);
        Assert.AreEqual(1, original.Content.Count);

        var contentAtHandoff = 0;
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Details))
            {
                contentAtHandoff = original.Content.Count;
            }
        };
        Assert.IsTrue(shell.TrySetDetails(replacement));
        Assert.AreEqual(1, contentAtHandoff);
        context.WaitForCleanup(original);
        text.Text = "No longer observed";
        Assert.AreEqual("Original", originalText.Text);
    }

    [TestMethod]
    public void DetailsRemoval_RetainsContentUntilHide_AndCannotBeShownAfterCleanup()
    {
        using var context = new TestContext();
        var model = new TestContentPage { Details = new ContentDetails { Content = [new TextContent("Original")] } };
        var page = context.CreateContentPage(model);
        var shell = context.CreateShell();
        var original = page.Details!;
        Assert.IsTrue(shell.TrySetDetails(original));

        model.Details = null;
        context.Drain();
        Assert.IsNull(page.Details);
        Assert.AreSame(original, shell.Details);
        Assert.AreEqual(1, original.Content.Count);

        Assert.IsTrue(shell.TrySetDetails(null));
        context.WaitForCleanup(original);
        Assert.IsFalse(shell.TrySetDetails(original));
        Assert.IsNull(shell.Details);
    }

    [TestMethod]
    public void DetailsReplacement_CleansSupersededCandidates_AndSkipsTheirQueuedRequests()
    {
        using var context = new TestContext();
        var model = new TestContentPage { Details = new ContentDetails { Content = [new TextContent("Original")] } };
        var page = context.CreateContentPage(model);
        var shell = context.CreateShell();
        var original = page.Details!;
        Assert.IsTrue(shell.TrySetDetails(original));
        var requests = context.CaptureDetailsRequests();

        model.Details = new ContentDetails { Content = [new TextContent("Skipped")] };
        var skipped = page.Details!;
        model.Details = new ContentDetails { Content = [new TextContent("Latest")] };
        context.Drain();

        Assert.AreSame(page.Details, requests.Single());
        Assert.AreEqual(0, skipped.Content.Count);
        Assert.IsFalse(shell.TrySetDetails(skipped));
        Assert.AreSame(original, shell.Details);
        Assert.AreEqual(1, original.Content.Count);
        Assert.IsTrue(shell.TrySetDetails(requests.Single()));
        context.WaitForCleanup(original);
    }

    [TestMethod]
    public void DetailsReplacement_InitializationFailureKeepsTheCurrentDetailsAndPendingPresentation()
    {
        using var context = new TestContext();
        var model = new TestContentPage { Details = new ContentDetails { Content = [new TextContent("Original")] } };
        var page = context.Own(new ContentPageViewModel(model, context.Scheduler, new TestAppExtensionHost(), CommandProviderContext.Empty));
        var requests = context.CaptureDetailsRequests();
        page.InitializeProperties();
        var original = page.Details;
        var failed = new CallbackDetails(() => throw new InvalidOperationException("Expected details failure"));

        model.Details = failed;
        context.Drain();

        Assert.AreSame(original, page.Details);
        Assert.AreEqual(1, original!.Content.Count);
        Assert.AreSame(original, requests.Single());
        var reads = failed.ContentReads;
        failed.Content = [new TextContent("After failure")];
        Assert.AreEqual(reads, failed.ContentReads);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DetailsReplacement_InFlightCandidateIsCleanedWhenSupersededOrStopped(bool stopPage)
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new TestContentPage { Details = new ContentDetails { Content = [new TextContent("Original")] } };
        var page = context.CreateContentPage(model);
        var requests = context.CaptureDetailsRequests();
        var candidate = new CallbackDetails(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        })
        {
            Content = [new TextContent("Abandoned")],
        };
        var pending = Task.Run(() => model.Details = candidate);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            if (stopPage)
            {
                page.SafeCleanup();
            }
            else
            {
                model.Details = new ContentDetails { Content = [new TextContent("Latest")] };
            }
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        if (stopPage)
        {
            Assert.IsNull(page.Details);
            Assert.AreEqual(0, requests.Count);
        }
        else
        {
            Assert.AreSame(page.Details, requests.Single());
            Assert.AreEqual("Latest", ((ContentTextViewModel)page.Details!.Content.Single()).Text);
        }

        var reads = candidate.ContentReads;
        candidate.Content = [new TextContent("After cleanup")];
        Assert.AreEqual(reads, candidate.ContentReads);
    }

    [TestMethod]
    public void DetailsCleanup_WaitsForEveryPresentationReference_AndDisposalIsIdempotent()
    {
        using var context = new TestContext();
        var vm = context.Own(new DetailsViewModel(new ContentDetails { Content = [new TextContent("Original")] }, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        using var first = vm.TryAcquirePresentation();
        using var second = vm.TryAcquirePresentation();
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);

        vm.SafeCleanup();
        vm.SafeCleanup();
        first.Dispose();
        first.Dispose();
        context.Drain();
        Assert.AreEqual(1, vm.Content.Count);

        second.Dispose();
        context.WaitForCleanup(vm);
        Assert.IsNull(vm.TryAcquirePresentation());
    }

    [TestMethod]
    public void DetailsCleanup_FinalPresentationReleaseDoesNotRevokeEventsOnTheUiThread()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var unsubscribeThread = 0;
        var tree = new SubscriptionTree
        {
            OnItemsUnsubscribe = () =>
            {
                unsubscribeThread = Environment.CurrentManagedThreadId;
                entered.Set();
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
            },
        };
        var vm = context.Own(new DetailsViewModel(new ContentDetails { Content = [tree] }, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        using var presentation = vm.TryAcquirePresentation();
        vm.SafeCleanup();
        try
        {
            presentation!.Dispose();
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreNotEqual(Environment.CurrentManagedThreadId, unsubscribeThread);
        }
        finally
        {
            release.Set();
        }

        context.WaitForCleanup(vm);
    }

    [TestMethod]
    public void CommandsRefresh_ConcurrentNotificationKeepsTheNewestSnapshot()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new CommandsContent();
        var vm = context.Own(new ContentCommandsViewModel(model, context.Reference));
        vm.InitializeProperties();
        var olderCommand = new CallbackCommand(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        })
        {
            Name = "Older",
        };
        var pending = Task.Run(() => model.Commands = [olderCommand]);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            model.Commands = [new NoOpCommand { Name = "Latest" }];
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        Assert.AreEqual("Latest", vm.Commands.Single().Name);
    }

    [TestMethod]
    public void SectionRefresh_NotificationDuringGetterKeepsTheNewestContent()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new CallbackSection { Content = [new TextContent("Older")] };
        var vm = context.Own(new ContentSectionViewModel(model, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        model.OnNextContentRead(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        });
        var pending = Task.Run(() => model.Title = "Changed title");
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            model.Content = [new TextContent("Latest")];
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        Assert.AreEqual("Latest", ((ContentTextViewModel)vm.VisibleContent.Single()).Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TreeRefresh_PropertyAndItemsNotificationsShareTheSameReadLoop(bool itemsFirst)
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new CallbackTree { Children = [new TextContent("Older")] };
        var vm = context.Own(new ContentTreeViewModel(model, context.Reference));
        vm.InitializeProperties();
        context.Drain();
        model.OnNextChildrenRead(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        });
        var pending = Task.Run(() =>
        {
            if (itemsFirst)
            {
                model.NotifyItemsChanged();
            }
            else
            {
                model.NotifyRootChanged();
            }
        });
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            model.Children = [new TextContent("Latest")];
            if (itemsFirst)
            {
                model.NotifyRootChanged();
            }
            else
            {
                model.NotifyItemsChanged();
            }
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        Assert.AreEqual("Latest", ((ContentTextViewModel)vm.Children.Single()).Text);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ContentRefresh_ReentrantNotificationDoesNotReenterGetters(bool duringInitialization)
    {
        using var context = new TestContext();
        var model = new CallbackText { Text = "Initial" };
        var vm = context.Own(new ContentTextViewModel(model, context.Reference));
        if (!duringInitialization)
        {
            vm.InitializeProperties();
        }

        model.OnNextRead(() => model.Text = "Latest");
        if (duringInitialization)
        {
            vm.InitializeProperties();
        }
        else
        {
            model.Text = "Older";
        }

        Assert.AreEqual("Latest", vm.Text);
        Assert.IsFalse(model.HadOverlappingReads);
    }

    [TestMethod]
    public void ContentRefresh_PendingNotificationSurvivesAReadFailure()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var errors = new ConcurrentQueue<Exception>();
        context.ExceptionHandler = errors.Enqueue;
        var model = new CallbackText { Text = "Initial" };
        var vm = context.Own(new ContentTextViewModel(model, context.Reference));
        vm.InitializeProperties();
        model.OnNextRead(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
            throw new InvalidOperationException("Expected refresh failure");
        });
        var pending = Task.Run(() => model.Text = "Fails");
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            model.Text = "Latest";
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.DrainUntil(() => vm.Text == "Latest");
        Assert.AreEqual("Expected refresh failure", errors.Single().Message);
        Assert.IsFalse(model.HadOverlappingReads);
    }

    [TestMethod]
    public void ContentRefresh_CleanupDropsQueuedNotifications()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new CallbackText { Text = "Initial" };
        var vm = context.Own(new ContentTextViewModel(model, context.Reference));
        vm.InitializeProperties();
        model.OnNextRead(() =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        });
        var pending = Task.Run(() => model.Text = "In flight");
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            model.Text = "Queued";
            vm.SafeCleanup();
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        model.Text = "After cleanup";
        context.Drain();
        Assert.AreEqual(2, model.ReadCount);
        Assert.IsFalse(model.HadOverlappingReads);
    }

    [TestMethod]
    public void FailedSnapshot_CleansNewObservers_AndRetainsPublishedSnapshot()
    {
        using var context = new TestContext();
        var probes = new List<ProbeViewModel>();
        var owner = context.Own(new ContentCollectionViewModel(context.Reference, (model, ctx) =>
        {
            var probe = new ProbeViewModel(ctx, model is SeparatorContent);
            probes.Add(probe);
            return probe;
        }));
        var original = new MarkdownContent();
        owner.Update([original]);
        context.Drain();

        Assert.ThrowsException<InvalidOperationException>(() => owner.Update([original, new HeaderContent(), new SeparatorContent()]));
        context.Drain();
        Assert.AreSame(probes[0], owner.Items.Single());
        Assert.AreEqual(0, probes[0].CleanupCount);
        Assert.AreEqual(1, probes[1].CleanupCount);
        Assert.AreEqual(1, probes[2].CleanupCount);
    }

    [TestMethod]
    public void CleanupBeforePublication_DoesNotRepopulateItems_AndRevokesCommands()
    {
        using var context = new TestContext();
        var command = new NoOpCommand { Name = "Original" };
        var commands = new ContentCommandsViewModel(new CommandsContent { Commands = [command] }, context.Reference);
        commands.InitializeProperties();
        var child = commands.Commands.Single();
        commands.SafeCleanup();
        command.Name = "Removed";
        Assert.AreEqual("Original", child.Name);

        var probe = new ProbeViewModel(context.Reference);
        var owner = context.Own(new ContentCollectionViewModel(context.Reference, (_, _) => probe));
        owner.Update([new MarkdownContent()]);
        owner.SafeCleanup();
        context.Drain();
        Assert.AreEqual(0, owner.Items.Count);
        Assert.AreEqual(1, probe.CleanupCount);
    }

    [TestMethod]
    public void SupersededInitialization_CannotOverwriteNewerSnapshot()
    {
        using var context = new TestContext();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var slow = new MarkdownContent();
        var slowProbe = new ProbeViewModel(context.Reference, onInitialize: () =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        });
        var newerProbe = new ProbeViewModel(context.Reference);
        var owner = context.Own(new ContentCollectionViewModel(context.Reference, (model, _) =>
            ReferenceEquals(model, slow) ? slowProbe : newerProbe));
        var pending = Task.Run(() => owner.Update([slow]));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            owner.Update([new SeparatorContent()]);
        }
        finally
        {
            release.Set();
            pending.GetAwaiter().GetResult();
        }

        context.Drain();
        Assert.AreSame(newerProbe, owner.Items.Single());
        Assert.AreEqual(1, slowProbe.CleanupCount);
    }

    [TestMethod]
    public async Task DeferredSample_LoadsOnRequestOnce_AndRetainsExpandedPreview()
    {
        using var context = new TestContext();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var details = new SampleDeferredDetails("Delayed sample", () => release.Task);
        Assert.AreEqual(0, details.LoadCount);
        try
        {
            var vm = context.Own(new DetailsViewModel(details, context.Reference));
            vm.InitializeProperties();
            context.Drain();
            var header = vm.Content[0];
            var preview = (ContentSectionViewModel)vm.Content[1];
            preview.IsExpanded = true;

            Parallel.For(0, 8, _ => details.GetContent());
            Assert.AreEqual(1, details.LoadCount);
            Assert.IsFalse(details.Completion.IsCompleted);

            release.SetResult();
            await details.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            context.Drain();
            Assert.AreSame(header, vm.Content[0]);
            Assert.AreSame(preview, vm.Content[1]);
            Assert.IsTrue(preview.IsExpanded);
            Assert.AreEqual(2, preview.VisibleContent.Count);
            Assert.IsTrue(vm.Content.OfType<ContentPropertyGridViewModel>().Any());

            var cached = details.Content;
            Assert.AreSame(cached, details.GetContent());
            Assert.AreEqual(1, details.LoadCount);
        }
        finally
        {
            release.TrySetResult();
            await details.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task DeferredSample_FailureWaitsForExplicitRetry()
    {
        var details = new SampleDeferredDetails("Retry sample", () => Task.CompletedTask, failFirstAttempt: true);
        var initial = details.GetContent();
        await details.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        var failed = details.Content;
        Assert.AreSame(initial[0], failed[0]);
        Assert.AreSame(initial[1], failed[1]);
        Assert.AreSame(failed, details.GetContent());
        Assert.AreEqual(1, details.LoadCount);

        var retry = (InvokableCommand)failed.OfType<CommandsContent>().Single().Commands.Single();
        Assert.AreEqual("Retry", retry.Name);
        retry.Invoke();
        await details.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, details.LoadCount);
        Assert.IsTrue(details.Content.OfType<PropertyGridContent>().Any());
        Assert.AreSame(initial[1], details.Content[1]);
        Assert.AreEqual("Load again", retry.Name);
    }

    private sealed partial class CallbackCommand(Action onNameRead) : NoOpCommand
    {
        public override string Name
        {
            get
            {
                onNameRead();
                return base.Name;
            }

            set => base.Name = value;
        }
    }

    private sealed partial class CallbackSection : SectionContent
    {
        private Action? _onNextRead;

        public override IContent[] Content
        {
            get
            {
                var snapshot = base.Content;
                Interlocked.Exchange(ref _onNextRead, null)?.Invoke();
                return snapshot;
            }

            set => base.Content = value;
        }

        public void OnNextContentRead(Action callback) => Interlocked.Exchange(ref _onNextRead, callback);
    }

    private sealed partial class CallbackTree : TreeContent
    {
        private Action? _onNextRead;

        public void OnNextChildrenRead(Action callback) => Interlocked.Exchange(ref _onNextRead, callback);

        public void NotifyItemsChanged() => RaiseItemsChanged();

        public void NotifyRootChanged() => OnPropertyChanged(nameof(RootContent));

        public override IContent[] GetChildren()
        {
            var snapshot = base.GetChildren();
            Interlocked.Exchange(ref _onNextRead, null)?.Invoke();
            return snapshot;
        }
    }

    private sealed partial class CallbackText : TextContent
    {
        private Action? _onNextRead;
        private int _activeReaders;
        private int _overlappingReads;
        private int _readCount;

        public bool HadOverlappingReads => Volatile.Read(ref _overlappingReads) != 0;

        public int ReadCount => Volatile.Read(ref _readCount);

        public override string Text
        {
            get
            {
                if (Interlocked.Increment(ref _activeReaders) > 1)
                {
                    Interlocked.Exchange(ref _overlappingReads, 1);
                }

                Interlocked.Increment(ref _readCount);
                try
                {
                    var snapshot = base.Text;
                    Interlocked.Exchange(ref _onNextRead, null)?.Invoke();
                    return snapshot;
                }
                finally
                {
                    Interlocked.Decrement(ref _activeReaders);
                }
            }

            set => base.Text = value;
        }

        public void OnNextRead(Action callback) => Interlocked.Exchange(ref _onNextRead, callback);
    }

    private sealed partial class SubscriptionTree : ITreeContent
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;
        private TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChanged;
        private int _childrenReads;

        public Action? OnItemsSubscribe { get; set; }

        public Action? OnItemsUnsubscribe { get; set; }

        public int ItemsAdds { get; private set; }

        public int ItemsRemoves { get; private set; }

        public int ChildrenReads => Volatile.Read(ref _childrenReads);

        public int PropertySubscribers => _propChanged?.GetInvocationList().Length ?? 0;

        public int ItemsSubscribers => _itemsChanged?.GetInvocationList().Length ?? 0;

        public IContent RootContent => null!;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove => _propChanged -= value;
        }

        public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged
        {
            add
            {
                ItemsAdds++;
                OnItemsSubscribe?.Invoke();
                _itemsChanged += value;
            }

            remove
            {
                _itemsChanged -= value;
                ItemsRemoves++;
                OnItemsUnsubscribe?.Invoke();
            }
        }

        public IContent[] GetChildren()
        {
            Interlocked.Increment(ref _childrenReads);
            return [];
        }

        public void NotifyRootChanged() => _propChanged?.Invoke(this, new PropChangedEventArgs(nameof(RootContent)));

        public Action CaptureItemsChanged()
        {
            var callback = _itemsChanged;
            return () => callback?.Invoke(this, new ItemsChangedEventArgs(0));
        }
    }

    private sealed partial class CallbackDetails(Action onGetContent) : ContentDetails
    {
        public int ContentReads { get; private set; }

        public override IContent[] GetContent()
        {
            ContentReads++;
            onGetContent();
            return Content;
        }
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Composed content tests";
    }

    private sealed partial class ContentOnlyDetails : ContentDetails
    {
        public override string Title { get => throw new InvalidOperationException("Legacy title fetched"); set => throw new NotSupportedException(); }

        public override string Body { get => throw new InvalidOperationException("Legacy body fetched"); set => throw new NotSupportedException(); }

        public override IIconInfo HeroImage { get => throw new InvalidOperationException("Legacy image fetched"); set => throw new NotSupportedException(); }

        public override IDetailsElement[] Metadata { get => throw new InvalidOperationException("Legacy metadata fetched"); set => throw new NotSupportedException(); }
    }

    private sealed partial class ProbeViewModel(WeakReference<IPageContext> context, bool fail = false, Action? onInitialize = null) : ContentViewModel(context)
    {
        public int CleanupCount { get; private set; }

        public override void InitializeProperties()
        {
            onInitialize?.Invoke();
            if (fail)
            {
                throw new InvalidOperationException("Expected initialization failure");
            }
        }

        protected override void UnsafeCleanup() => CleanupCount++;
    }

    private sealed class TestContext : IPageContext, IDisposable
    {
        private readonly QueuedScheduler _scheduler = new();
        private readonly List<ExtensionObjectViewModel> _owned = [];
        private readonly List<ShellViewModel> _shells = [];

        public TaskScheduler Scheduler => _scheduler;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public WeakReference<IPageContext> Reference => new(this);

        public Action<Exception>? ExceptionHandler { get; set; }

        public T Own<T>(T vm)
            where T : ExtensionObjectViewModel
        {
            _owned.Add(vm);
            return vm;
        }

        public ContentPageViewModel CreateContentPage(TestContentPage model)
        {
            var page = Own(new ContentPageViewModel(model, Scheduler, new TestAppExtensionHost(), ProviderContext));
            page.InitializeProperties();
            Drain();
            return page;
        }

        public ShellViewModel CreateShell()
        {
            var hosts = new Mock<IAppHostService>();
            hosts.Setup(service => service.GetDefaultHost()).Returns(new TestAppExtensionHost());
            var shell = new ShellViewModel(Scheduler, Mock.Of<IRootPageService>(), Mock.Of<IPageViewModelFactoryService>(), hosts.Object);
            _shells.Add(shell);
            return shell;
        }

        public List<DetailsViewModel> CaptureDetailsRequests()
        {
            var requests = new List<DetailsViewModel>();
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(this, (_, message) => requests.Add(message.Details));
            return requests;
        }

        public void WaitForCleanup(DetailsViewModel details) => DrainUntil(() => details.Content.Count == 0);

        public void DrainUntil(Func<bool> condition)
        {
            var completed = SpinWait.SpinUntil(
                () =>
                {
                    Drain();
                    return condition();
                },
                TimeSpan.FromSeconds(5));
            Assert.IsTrue(completed, "Queued work did not finish.");
        }

        public void Drain() => _scheduler.Drain();

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            if (ExceptionHandler is { } handler)
            {
                handler(ex);
            }
            else
            {
                throw new AssertFailedException(ex.ToString());
            }
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            foreach (var shell in _shells)
            {
                WeakReferenceMessenger.Default.UnregisterAll(shell);
                shell.Dispose();
            }

            foreach (var vm in _owned)
            {
                vm.SafeCleanup();
            }

            Drain();
        }
    }

    private sealed class QueuedScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = [];

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void Drain()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                if (task.Exception is { } error)
                {
                    throw error;
                }
            }
        }
    }
}
