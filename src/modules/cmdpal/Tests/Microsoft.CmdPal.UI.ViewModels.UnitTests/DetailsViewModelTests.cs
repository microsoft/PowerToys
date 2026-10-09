// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
