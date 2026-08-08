// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class TabbedPageTests
{
    private sealed partial class TestTabbedPage : TabbedPage
    {
        private ITab[] _tabs;

        public TestTabbedPage(ITab[] tabs)
        {
            _tabs = tabs;
        }

        public override ITab[] GetTabs() => _tabs;

        public void SetTabs(ITab[] tabs)
        {
            _tabs = tabs;
            RaiseItemsChanged(tabs.Length);
        }
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    [TestMethod]
    public void Tab_HasExpectedDefaults()
    {
        var tab = new Tab();

        Assert.AreEqual(string.Empty, tab.Title);
        Assert.AreEqual(string.Empty, tab.Badge);
        Assert.IsNull(tab.Icon);
        Assert.IsNull(tab.Page);
    }

    [TestMethod]
    public void Tab_TitleAndPageConstructor_SetsBoth()
    {
        var page = new TestContentPage();
        var tab = new Tab("Issues", page);

        Assert.AreEqual("Issues", tab.Title);
        Assert.AreSame(page, tab.Page);
    }

    [TestMethod]
    public void Tab_Badge_RaisesPropChanged()
    {
        var tab = new Tab();
        string? changed = null;
        tab.PropChanged += (s, e) => changed = e.PropertyName;

        tab.Badge = "10";

        Assert.AreEqual("10", tab.Badge);
        Assert.AreEqual(nameof(Tab.Badge), changed);
    }

    [TestMethod]
    public void TabbedPage_ImplementsBothTabbedAndContentInterfaces()
    {
        var page = new TestTabbedPage([]);

        Assert.IsInstanceOfType(page, typeof(ITabbedPage));
        Assert.IsInstanceOfType(page, typeof(IContentPage));
    }

    [TestMethod]
    public void TabbedPage_GetTabs_ReturnsProvidedTabs()
    {
        var tabs = new ITab[]
        {
            new Tab("One", new TestContentPage()),
            new Tab("Two", new TestContentPage()),
        };
        var page = new TestTabbedPage(tabs);

        var result = page.GetTabs();

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("One", result[0].Title);
        Assert.AreEqual("Two", result[1].Title);
    }

    [TestMethod]
    public void TabbedPage_FallbackContent_UsesUpdateMessage()
    {
        var page = new TestTabbedPage([]);

        var content = page.GetContent();

        Assert.AreEqual(1, content.Length);
        var markdown = content[0] as IMarkdownContent;
        Assert.IsNotNull(markdown);
        Assert.AreEqual(page.UpdateMessage, markdown!.Body);
        Assert.IsFalse(string.IsNullOrWhiteSpace(markdown.Body));
    }

    [TestMethod]
    public void TabbedPage_FallbackContent_HonorsCustomUpdateMessage()
    {
        var page = new TestTabbedPage([])
        {
            UpdateMessage = "Custom please-update text",
        };

        var content = page.GetContent();
        var markdown = content[0] as IMarkdownContent;

        Assert.IsNotNull(markdown);
        Assert.AreEqual("Custom please-update text", markdown!.Body);
    }

    [TestMethod]
    public void TabbedPage_RaiseItemsChanged_FiresEvent()
    {
        var page = new TestTabbedPage([]);
        var raised = false;
        page.ItemsChanged += (s, e) => raised = true;

        page.SetTabs([new Tab("New", new TestContentPage())]);

        Assert.IsTrue(raised);
        Assert.AreEqual(1, page.GetTabs().Length);
    }
}
