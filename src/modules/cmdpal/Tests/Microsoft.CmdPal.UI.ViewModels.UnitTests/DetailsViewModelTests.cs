// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class DetailsViewModelTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private static WeakReference<IPageContext> CreatePageContext()
    {
        var ctx = new TestPageContext();
        return new WeakReference<IPageContext>(ctx);
    }

    [TestMethod]
    public void InitializeProperties_SetsBodyAndTitle()
    {
        var details = new Details { Title = "Hello", Body = "World" };
        var vm = new DetailsViewModel(details, CreatePageContext());

        vm.InitializeProperties();

        Assert.AreEqual("Hello", vm.Title);
        Assert.AreEqual("World", vm.Body);
    }

    [TestMethod]
    public void PropChanged_Body_UpdatesViewModelProperty()
    {
        var details = new Details { Title = "Initial", Body = "Initial body" };
        var vm = new DetailsViewModel(details, CreatePageContext());
        vm.InitializeProperties();

        // Act — toolkit Details raises PropChanged synchronously on set
        details.Body = "Updated body";

        // The property value is set synchronously in FetchProperty;
        // ApplyPendingUpdates flushes the PropertyChanged notification queue.
        vm.ApplyPendingUpdates();

        Assert.AreEqual("Updated body", vm.Body);
    }

    [TestMethod]
    public void PropChanged_Title_UpdatesViewModelProperty()
    {
        var details = new Details { Title = "Original", Body = "Text" };
        var vm = new DetailsViewModel(details, CreatePageContext());
        vm.InitializeProperties();

        details.Title = "New Title";
        vm.ApplyPendingUpdates();

        Assert.AreEqual("New Title", vm.Title);
    }

    [TestMethod]
    public void PropChanged_Metadata_RebuildsList()
    {
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata = [],
        };
        var vm = new DetailsViewModel(details, CreatePageContext());
        vm.InitializeProperties();
        Assert.AreEqual(0, vm.Metadata.Count);

        // Act — update metadata with a link element
        details.Metadata = [new DetailsElement { Key = "link", Data = new DetailsLink("http://example.com", "Example") }];
        vm.ApplyPendingUpdates();

        Assert.AreEqual(1, vm.Metadata.Count);
    }

    [TestMethod]
    public void Cleanup_UnsubscribesFromPropChanged()
    {
        var details = new Details { Title = "T", Body = "Original" };
        var vm = new DetailsViewModel(details, CreatePageContext());
        vm.InitializeProperties();

        // Act — cleanup unsubscribes, then change should not propagate
        vm.SafeCleanup();
        details.Body = "After cleanup";

        Assert.AreEqual("Original", vm.Body);
    }

    [TestMethod]
    public void NonObservableDetails_DoesNotThrow()
    {
        // IDetails that does NOT implement INotifyPropChanged
        var details = new NonObservableDetails();
        var vm = new DetailsViewModel(details, CreatePageContext());

        // Should not throw — just doesn't subscribe to anything
        vm.InitializeProperties();

        Assert.AreEqual("Static Title", vm.Title);
        Assert.AreEqual("Static Body", vm.Body);
    }

    [TestMethod]
    public void Cleanup_ReleasesMetadataCommandViewModels()
    {
        // The extension-side command is the root: the CommandViewModel built for
        // it subscribes to PropChanged, so an unrevoked handler outlives the
        // whole details pane.
        var pageContext = new TestPageContext();
        var command = new Command { Name = "Run it" };
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata = [new DetailsElement { Key = "commands", Data = new DetailsCommands { Commands = [command] } }],
        };

        var weakCommandVm = BuildInitializeAndCleanup(details, pageContext);

        GcAssert.IsCollected(weakCommandVm, "CommandViewModel from details metadata");

        GC.KeepAlive(command);
        GC.KeepAlive(details);
        GC.KeepAlive(pageContext);
    }

    [TestMethod]
    public void Cleanup_ReleasesMetadataElementsWithoutSubscriptions()
    {
        // Baseline for the GC harness: a link element subscribes to nothing, so
        // it is collectable regardless of the metadata walk. If this one fails,
        // the measurement is wrong rather than the product code.
        var pageContext = new TestPageContext();
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata = [new DetailsElement { Key = "link", Data = new DetailsLink("http://example.com", "Example") }],
        };

        var weakElementVm = BuildInitializeAndCleanupElement(details, pageContext);

        GcAssert.IsCollected(weakElementVm, "DetailsLinkViewModel");

        GC.KeepAlive(details);
        GC.KeepAlive(pageContext);
    }

    [TestMethod]
    public void MetadataRebuild_ThatThrows_ReleasesPartiallyBuiltElements()
    {
        // The first element builds fine and subscribes; reading the second one
        // fails, the way a dying extension would. The first must not be left
        // attached just because the rebuild never completed.
        var pageContext = new TestPageContext();
        var command = new HandlerCountingCommand();
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata =
            [
                new DetailsElement { Key = "commands", Data = new DetailsCommands { Commands = [command] } },
                new ThrowingDetailsElement(),
            ],
        };

        var vm = new DetailsViewModel(details, new(pageContext));

        Assert.ThrowsException<InvalidOperationException>(() => vm.InitializeProperties());

        Assert.AreEqual(0, command.HandlerCount, "an element built before the failure was left subscribed");
    }

    [TestMethod]
    public void MetadataRebuild_WhenCommandInitializationThrows_ReleasesPartiallyBuiltCommands()
    {
        var pageContext = new TestPageContext();
        var initializedCommand = new HandlerCountingCommand();
        var failingCommand = new ThrowingOnSubscribeCommand();
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata =
            [
                new DetailsElement
                {
                    Key = "commands",
                    Data = new DetailsCommands { Commands = [initializedCommand, failingCommand] },
                },
            ],
        };

        var vm = new DetailsViewModel(details, new(pageContext));

        Assert.ThrowsException<InvalidOperationException>(() => vm.InitializeProperties());

        Assert.AreEqual(0, initializedCommand.HandlerCount, "a command initialized before the failure was left subscribed");
        Assert.AreEqual(0, failingCommand.HandlerCount, "the command whose subscription failed was left subscribed");

        vm.SafeCleanup();
    }

    /// <summary>
    /// Reports whether anything is still subscribed - a view-model that was
    /// dropped without cleanup shows up here as a handler that was never revoked.
    /// </summary>
    private sealed partial class HandlerCountingCommand : ICommand
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add => _propChanged += value;
            remove => _propChanged -= value;
        }

        public int HandlerCount => _propChanged?.GetInvocationList().Length ?? 0;

        public string Name => "Counting";

        public string Id => "test.counting";

        public IIconInfo Icon => new IconInfo(string.Empty);
    }

    private sealed partial class ThrowingOnSubscribeCommand : ICommand
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add
            {
                _propChanged += value;
                throw new InvalidOperationException("Extension went away while subscribing");
            }

            remove => _propChanged -= value;
        }

        public int HandlerCount => _propChanged?.GetInvocationList().Length ?? 0;

        public string Name => "Throwing";

        public string Id => "test.throwing";

        public IIconInfo Icon => new IconInfo(string.Empty);
    }

    /// <summary>
    /// An element whose data cannot be read, standing in for an extension that
    /// went away mid-rebuild.
    /// </summary>
    private sealed partial class ThrowingDetailsElement : IDetailsElement
    {
        public string Key => "boom";

        public IDetailsData? Data => throw new InvalidOperationException("Extension went away");
    }

    [TestMethod]
    public void MetadataRebuild_ReleasesDisplacedCommandViewModels()
    {
        // No cleanup call here: the details pane stays live and only its
        // metadata is swapped, which is the path that used to drop the previous
        // elements without revoking their handlers.
        var pageContext = new TestPageContext();
        var command = new Command { Name = "Run it" };
        var details = new Details
        {
            Title = "T",
            Body = "B",
            Metadata = [new DetailsElement { Key = "commands", Data = new DetailsCommands { Commands = [command] } }],
        };

        var weakDisplacedVm = BuildInitializeAndReplaceMetadata(details, pageContext);

        GcAssert.IsCollected(weakDisplacedVm, "CommandViewModel displaced by a metadata rebuild");

        GC.KeepAlive(command);
        GC.KeepAlive(details);
        GC.KeepAlive(pageContext);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CommandViewModel> BuildInitializeAndReplaceMetadata(Details details, IPageContext pageContext)
    {
        var vm = new DetailsViewModel(details, new(pageContext));
        vm.InitializeProperties();

        var weak = new WeakReference<CommandViewModel>(((DetailsCommandsViewModel)vm.Metadata[0]).Commands[0]);

        // Toolkit Details raises PropChanged synchronously, so this rebuilds now.
        // The view-model itself stays reachable through that subscription.
        details.Metadata = [new DetailsElement { Key = "link", Data = new DetailsLink("http://example.com", "Example") }];

        return weak;
    }

    // Separate frames so the view-models are unreachable on return - a Debug
    // build keeps locals alive to the end of their scope.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<CommandViewModel> BuildInitializeAndCleanup(IDetails details, IPageContext pageContext)
    {
        var vm = new DetailsViewModel(details, new(pageContext));
        vm.InitializeProperties();

        var commandsVm = (DetailsCommandsViewModel)vm.Metadata[0];
        var weak = new WeakReference<CommandViewModel>(commandsVm.Commands[0]);

        vm.SafeCleanup();
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<DetailsElementViewModel> BuildInitializeAndCleanupElement(IDetails details, IPageContext pageContext)
    {
        var vm = new DetailsViewModel(details, new(pageContext));
        vm.InitializeProperties();

        var weak = new WeakReference<DetailsElementViewModel>(vm.Metadata[0]);

        vm.SafeCleanup();
        return weak;
    }

    /// <summary>
    /// A minimal IDetails that does NOT implement INotifyPropChanged.
    /// </summary>
    private sealed partial class NonObservableDetails : IDetails
    {
        public IIconInfo HeroImage => new IconInfo(string.Empty);

        public string Title => "Static Title";

        public string Body => "Static Body";

        public IDetailsElement[] Metadata => [];
    }
}
