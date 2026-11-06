// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    private sealed partial class LegacyGalleryLayout : BaseObservable, IGalleryGridLayout
    {
        public bool ShowTitle => false;

        public bool ShowSubtitle => true;
    }

    private sealed partial class AttributedGalleryLayout : GalleryGridLayout
    {
        public IDictionary<string, object>? Properties { get; set; }

        public int PropertyReads { get; private set; }

        public override IDictionary<string, object>? GetProperties()
        {
            PropertyReads++;
            return Properties;
        }
    }

    [TestMethod]
    public void GalleryLayoutDefaultsToCropToFill()
    {
        var layout = new GalleryGridLayout();
        IGalleryGridLayout legacyInterface = layout;
        var viewModel = new GalleryGridPropertiesViewModel(legacyInterface);

        viewModel.InitializeProperties();

        Assert.AreEqual(GalleryImageFit.Cover, layout.ImageFit);
        Assert.IsTrue(viewModel.ShowTitle);
        Assert.IsTrue(viewModel.ShowSubtitle);
        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
    }

    [TestMethod]
    public void LegacyGalleryLayoutKeepsCropToFill()
    {
        IGalleryGridLayout layout = new LegacyGalleryLayout();
        var viewModel = new GalleryGridPropertiesViewModel(layout);

        viewModel.InitializeProperties();

        Assert.IsFalse(layout is IExtendedAttributesProvider);
        Assert.AreEqual(new Guid("E2CD9402-042B-5BD3-92D5-E81B48F39024"), typeof(IGalleryGridLayout).GUID);
        Assert.IsFalse(viewModel.ShowTitle);
        Assert.IsTrue(viewModel.ShowSubtitle);
        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
    }

    [DataTestMethod]
    [DataRow(GalleryImageFit.Cover, Stretch.UniformToFill, 0)]
    [DataRow(GalleryImageFit.Contain, Stretch.Uniform, 1)]
    public void GalleryImageFitMapsToImageStretch(GalleryImageFit imageFit, Stretch expected, int expectedAttribute)
    {
        var layout = new GalleryGridLayout { ImageFit = imageFit };
        var viewModel = new GalleryGridPropertiesViewModel(layout);

        viewModel.InitializeProperties();

        var properties = ((IExtendedAttributesProvider)layout).GetProperties();
        Assert.IsNotNull(properties);
        Assert.IsInstanceOfType<int>(properties[WellKnownExtensionAttributes.GalleryImageFit], out var attribute);
        Assert.AreEqual(expectedAttribute, attribute);
        Assert.AreEqual(expected, viewModel.ImageStretch);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GalleryImageFitDefaultsToCoverWhenAttributeMissing(bool nullProperties)
    {
        var layout = new AttributedGalleryLayout
        {
            Properties = nullProperties ? null : new Dictionary<string, object>(),
        };
        var viewModel = new GalleryGridPropertiesViewModel(layout);

        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
        Assert.AreEqual(1, layout.PropertyReads);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("Contain")]
    [DataRow(true)]
    [DataRow(1L)]
    [DataRow(GalleryImageFit.Contain)]
    public void GalleryImageFitIgnoresMalformedAttributes(object? attribute)
    {
        var layout = new AttributedGalleryLayout
        {
            Properties = new Dictionary<string, object>
            {
                [WellKnownExtensionAttributes.GalleryImageFit] = attribute!,
            },
        };
        var viewModel = new GalleryGridPropertiesViewModel(layout);

        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
        Assert.AreEqual(1, layout.PropertyReads);
    }

    [TestMethod]
    public void GalleryImageFitReadsAndClearsCustomAttributes()
    {
        var layout = new AttributedGalleryLayout
        {
            Properties = new Dictionary<string, object>
            {
                [WellKnownExtensionAttributes.GalleryImageFit] = 1,
            },
        };
        var viewModel = new GalleryGridPropertiesViewModel(layout);

        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.Uniform, viewModel.ImageStretch);
        Assert.AreEqual(1, layout.PropertyReads);

        layout.Properties.Clear();
        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
        Assert.AreEqual(2, layout.PropertyReads);
    }

    [TestMethod]
    public void GalleryImageFitCanReturnToCover()
    {
        var layout = new GalleryGridLayout { ImageFit = GalleryImageFit.Contain };
        var viewModel = new GalleryGridPropertiesViewModel(layout);
        viewModel.InitializeProperties();
        Assert.AreEqual(Stretch.Uniform, viewModel.ImageStretch);

        layout.ImageFit = GalleryImageFit.Cover;
        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void GalleryImageFitFallsBackToCoverForUnknownValues(int attribute)
    {
        var layout = new AttributedGalleryLayout
        {
            Properties = new Dictionary<string, object>
            {
                [WellKnownExtensionAttributes.GalleryImageFit] = 1,
            },
        };
        var viewModel = new GalleryGridPropertiesViewModel(layout);
        viewModel.InitializeProperties();
        Assert.AreEqual(Stretch.Uniform, viewModel.ImageStretch);

        layout.Properties[WellKnownExtensionAttributes.GalleryImageFit] = attribute;
        viewModel.InitializeProperties();

        Assert.AreEqual(Stretch.UniformToFill, viewModel.ImageStretch);
        Assert.AreEqual(2, layout.PropertyReads);
    }

    [DataTestMethod]
    [DataRow(GalleryImageFit.Cover, Stretch.UniformToFill)]
    [DataRow(GalleryImageFit.Contain, Stretch.Uniform)]
    public async Task GalleryImageFitAppliesToNewAndCachedItems(GalleryImageFit imageFit, Stretch expected)
    {
        var first = new ListItem(new NoOpCommand { Name = "First" });
        var page = new SearchableDynamicPage
        {
            GridProperties = new GalleryGridLayout { ImageFit = imageFit },
            Items = [first],
        };
        var viewModel = CreateViewModel(page);
        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            var firstViewModel = viewModel.FilteredItems[0];
            Assert.AreEqual(expected, firstViewModel.LayoutImageStretch);

            page.Items = [first, new ListItem(new NoOpCommand { Name = "Second" })];
            await ObserveNextItemsUpdateAsync(viewModel, () => page.TriggerItemsChanged(2));

            Assert.AreEqual(2, viewModel.FilteredItems.Count);
            Assert.AreSame(firstViewModel, viewModel.FilteredItems[0]);
            Assert.AreEqual(expected, viewModel.FilteredItems[0].LayoutImageStretch);
            Assert.AreEqual(expected, viewModel.FilteredItems[1].LayoutImageStretch);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    public async Task GalleryLayoutReplacementUpdatesExistingItems()
    {
        var page = new SearchableDynamicPage { GridProperties = new GalleryGridLayout() };
        var viewModel = CreateViewModel(page);
        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            var item = viewModel.FilteredItems[0];
            Assert.AreEqual(Stretch.UniformToFill, item.LayoutImageStretch);
            item.ApplyPendingUpdates();
            var stretchChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ListItemViewModel.LayoutImageStretch))
                {
                    stretchChanged.TrySetResult();
                }
            };

            page.GridProperties = new GalleryGridLayout { ImageFit = GalleryImageFit.Contain };
            item.ApplyPendingUpdates();
            await stretchChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(Stretch.Uniform, item.LayoutImageStretch);
            Assert.AreSame(item, viewModel.FilteredItems[0]);

            page.GridProperties = new LegacyGalleryLayout();

            Assert.AreEqual(Stretch.UniformToFill, item.LayoutImageStretch);
            Assert.AreSame(item, viewModel.FilteredItems[0]);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }
}
