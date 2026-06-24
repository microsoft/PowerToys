// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace SamplePagesExtension;

internal sealed partial class SampleDataTransferPage : ListPage
{
    private readonly IListItem[] _items;

    public SampleDataTransferPage()
    {
        var dataPackageWithText = CreateDataPackageWithText();
        var dataPackageWithDelayedText = CreateDataPackageWithDelayedText();
        var dataPackageWithImage = CreateDataPackageWithImage();

        _items =
        [
            new ListItem(new NoOpCommand())
            {
                Title = "Draggable item with a plain text",
                Subtitle = "A sample page demonstrating how to drag and drop data",
                DataPackage = dataPackageWithText,
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Draggable item with a lazily rendered plain text",
                Subtitle = "A sample page demonstrating how to drag and drop data with delayed rendering",
                DataPackage = dataPackageWithDelayedText,
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Draggable item with an image",
                Subtitle = "This item has an image - package contains both file and a bitmap",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
                DataPackage = dataPackageWithImage,
            },
            new ListItem(new SampleDataTransferOnGridPage())
            {
                Title = "Drag & drop grid",
                Subtitle = "A sample page demonstrating a grid list of items",
                Icon = new IconInfo("\uF0E2"),
            }
        ];
    }

    private static DataPackage CreateDataPackageWithText()
    {
        var dataPackageWithText = new DataPackage
        {
            Properties =
            {
                Title = "Item with data package with text",
                Description = "This item has associated text with it",
            },
            RequestedOperation = DataPackageOperation.Copy,
        };
        dataPackageWithText.SetText("Text data in the Data Package");
        return dataPackageWithText;
    }

    private static DataPackage CreateDataPackageWithDelayedText()
    {
        var dataPackageWithDelayedText = new DataPackage
        {
            Properties =
            {
                Title = "Item with delayed render data in the data package",
                Description = "This items has an item associated with it that is evaluated when requested for the first time",
            },
            RequestedOperation = DataPackageOperation.Copy,
        };
        dataPackageWithDelayedText.SetDataProvider(StandardDataFormats.Text, request =>
        {
            var d = request.GetDeferral();
            try
            {
                request.SetData(DateTime.Now.ToString("G", CultureInfo.CurrentCulture));
            }
            finally
            {
                d.Complete();
            }
        });
        return dataPackageWithDelayedText;
    }

    private static DataPackage CreateDataPackageWithImage()
    {
        var dataPackageWithImage = new DataPackage
        {
            Properties =
            {
                Title = "Item with delayed render image in the data package",
                Description = "This items has an image associated with it that is evaluated when requested for the first time",
            },
            RequestedOperation = DataPackageOperation.Copy,
        };
        dataPackageWithImage.SetDataProvider(StandardDataFormats.Bitmap, async void (request) =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Images/Swirls.png"));
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var streamRef = RandomAccessStreamReference.CreateFromStream(stream);
                request.SetData(streamRef);
            }
            finally
            {
                deferral.Complete();
            }
        });
        dataPackageWithImage.SetDataProvider(StandardDataFormats.StorageItems, async void (request) =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Images/Swirls.png"));
                var items = new[] { file };
                request.SetData(items);
            }
            finally
            {
                deferral.Complete();
            }
        });
        return dataPackageWithImage;
    }

    public override IListItem[] GetItems() => _items;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Samples")]
internal sealed partial class SampleDataTransferOnGridPage : ListPage
{
    public SampleDataTransferOnGridPage()
    {
        GridProperties = new GalleryGridLayout
        {
            ShowTitle = true,
            ShowSubtitle = true,
        };
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand())
            {
                Title = "Red Rectangle",
                Subtitle = "Drag me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/RedRectangle.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Swirls",
                Subtitle = "Drop me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/Swirls.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Windows Digital",
                Subtitle = "Drag me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/Win-Digital.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Red Rectangle",
                Subtitle = "Drop me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/RedRectangle.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/RedRectangle.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Space",
                Subtitle = "Drag me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Space.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/Space.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Swirls",
                Subtitle = "Drop me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Swirls.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/Swirls.png"),
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Windows Digital",
                Subtitle = "Drag me",
                Icon = IconHelpers.FromRelativePath("Assets/Images/Win-Digital.png"),
                DataPackage = CreateDataPackageForImage("Assets/Images/Win-Digital.png"),
            },
        ];
    }

    private static DataPackage CreateDataPackageForImage(string relativePath)
    {
        var dataPackageWithImage = new DataPackage
        {
            Properties =
            {
                Title = "Image",
                Description = "This item has an image associated with it.",
            },
            RequestedOperation = DataPackageOperation.Copy,
        };

        var imageUri = new Uri($"ms-appx:///{relativePath}");

        dataPackageWithImage.SetDataProvider(StandardDataFormats.Bitmap, async (request) =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(imageUri);
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var streamRef = RandomAccessStreamReference.CreateFromStream(stream);
                request.SetData(streamRef);
            }
            finally
            {
                deferral.Complete();
            }
        });

        dataPackageWithImage.SetDataProvider(StandardDataFormats.StorageItems, async (request) =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(imageUri);
                var items = new[] { file };
                request.SetData(items);
            }
            finally
            {
                deferral.Complete();
            }
        });
        return dataPackageWithImage;
    }
}
