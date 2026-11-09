// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class PerformanceSmallContentTests
{
    [TestMethod]
    public void PropChanged_ActivatesUntilLastSubscriptionIsRemoved()
    {
        var widget = new TrackingWidget();
        var content = new PerformanceSmallContent(widget, new Size(360, 180));
        var updates = 0;
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, args) =>
        {
            Assert.AreEqual(nameof(IFormContent.DataJson), args.PropertyName);
            updates++;
        };
        content.PropChanged -= handler;
        Assert.AreEqual(0, widget.Activations);

        content.PropChanged += handler;
        content.PropChanged += handler;
        widget.UpdateWidget();
        content.PropChanged -= handler;
        Assert.AreEqual(1, widget.Activations);
        Assert.AreEqual(0, widget.Deactivations);
        Assert.AreEqual(2, updates);

        content.PropChanged -= handler;
        content.PropChanged -= handler;
        widget.UpdateWidget();
        Assert.AreEqual(1, widget.Deactivations);
        Assert.AreEqual(2, updates);
    }

    [TestMethod]
    public void PropChanged_DockAndTrayShareTheWidgetWithoutPrematurelyStoppingIt()
    {
        var widget = new TrackingWidget();
        var first = new PerformanceSmallContent(widget, new Size(360, 180));
        var second = new PerformanceSmallContent(widget, new Size(360, 180));
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, _) => { };
        widget.PushActivate();
        first.PropChanged += handler;
        second.PropChanged += handler;
        first.PropChanged -= handler;
        second.PropChanged -= handler;
        Assert.AreEqual(1, widget.Activations);
        Assert.AreEqual(0, widget.Deactivations);

        widget.PopActivate();
        Assert.AreEqual(1, widget.Deactivations);
    }

    [TestMethod]
    [DataRow(40, "60px")]
    [DataRow(200, "92px")]
    [DataRow(336, "160px")]
    [DataRow(1000, "160px")]
    public void Template_UsesAdvisoryWidthWithinCompactBounds(int width, string expectedWidth)
    {
        var content = new PerformanceSmallContent(new TrackingWidget(), new Size(width, 100));
        using var card = JsonDocument.Parse(content.TemplateJson);
        var columns = card.RootElement.GetProperty("body")[0].GetProperty("columns");
        var image = columns[1].GetProperty("items")[0];

        Assert.AreEqual("${hasGraph}", columns[1].GetProperty("$when").GetString());
        Assert.AreEqual("${value}", columns[0].GetProperty("items")[1].GetProperty("text").GetString());
        foreach (var text in columns[0].GetProperty("items").EnumerateArray().Skip(1).Take(2))
        {
            Assert.IsTrue(text.GetProperty("wrap").GetBoolean());
            Assert.AreEqual(2, text.GetProperty("maxLines").GetInt32());
        }

        Assert.AreEqual(expectedWidth, image.GetProperty("width").GetString());
        Assert.AreEqual("46px", image.GetProperty("height").GetString());
        Assert.AreEqual(CommandResultKind.KeepOpen, content.SubmitForm("{}", "{}").Kind);
    }

    private sealed partial class TrackingWidget : WidgetPage
    {
        public int Activations { get; private set; }

        public int Deactivations { get; private set; }

        protected override void LoadContentData()
        {
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;

        protected override void OnActivated() => Activations++;

        protected override void OnDeactivated() => Deactivations++;
    }
}
