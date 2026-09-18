// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public sealed class WidgetContentTests
{
    [TestMethod]
    public void DefaultsAreSafe()
    {
        var widget = new WidgetContent();

        Assert.AreEqual(string.Empty, widget.Id);
        Assert.AreEqual(string.Empty, widget.Title);
        Assert.AreEqual(string.Empty, widget.Description);
        Assert.IsNotNull(widget.Icon);
        Assert.AreEqual(0, widget.SupportedSizes.Length);
        Assert.IsFalse(widget.AllowMultiple);
        Assert.IsNull(widget.Content);
    }

    [TestMethod]
    public void MetadataRaisesPropertyChanged()
    {
        var widget = new WidgetContent();
        var changedProperties = new List<string>();
        widget.PropChanged += (_, args) => changedProperties.Add(args.PropertyName);

        widget.Id = "sample.widget";
        widget.Title = "Sample widget";
        widget.AllowMultiple = true;
        widget.Content = new FormContent();

        CollectionAssert.AreEqual(
            new[] { nameof(widget.Id), nameof(widget.Title), nameof(widget.AllowMultiple), nameof(widget.Content) },
            changedProperties);
    }

    [TestMethod]
    public void LifecycleMethodsCanBeOverridden()
    {
        var widget = new TrackingWidget();

        widget.Activate();
        widget.Deactivate();
        widget.Delete();

        Assert.AreEqual(1, widget.ActivationCount);
        Assert.AreEqual(1, widget.DeactivationCount);
        Assert.AreEqual(1, widget.DeleteCount);
    }

    private sealed partial class TrackingWidget : WidgetContent
    {
        public int ActivationCount { get; private set; }

        public int DeactivationCount { get; private set; }

        public int DeleteCount { get; private set; }

        public override void Activate() => ActivationCount++;

        public override void Deactivate() => DeactivationCount++;

        public override void Delete() => DeleteCount++;
    }
}
